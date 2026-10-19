using Pausengong.Core.Config;
using Pausengong.Core.Models;

namespace Pausengong.Core.Scheduling;

/// <summary>
/// Drives the per-room draw/fallback timeline for every scheduled break time: a T-60s draw
/// among currently-available room members, a T-10s recheck that replaces the pick if it went
/// offline meanwhile, and a T-0 ring with confirm-or-fallback through the rest of the drawn
/// candidate order. Only runs anything while <c>isSelfChief</c> is true — the Chef is the single
/// authority for the draw so nodes don't race each other; MQTT wiring lives one layer up in
/// <see cref="RingCoordinatorService"/>, which is why every dependency here is a plain delegate
/// (send/wait/availability) rather than a concrete MQTT type — that's what makes this fully
/// unit-testable with a <see cref="TimeProvider"/> and faked delegates, no real network/timers.
/// </summary>
public sealed class BreakScheduler : IDisposable
{
    private static readonly TimeSpan DrawLeadTime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RecheckLeadTime = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(5);

    // Must be smaller than the smallest gap between stages (10s, between recheck and ring) so
    // the fire-windows below never overlap, and bigger than the poll interval so a tick can't
    // slip through the gap between two polls.
    private static readonly TimeSpan FireWindow = TimeSpan.FromSeconds(8);

    private readonly ConfigStore _configStore;
    private readonly Func<IReadOnlySet<string>> _getAvailableNodeIds;
    private readonly Func<bool> _isSelfChief;
    private readonly Func<string, string, DateTimeOffset, CancellationToken, Task> _sendRingCommandAsync;
    private readonly Func<string, TimeSpan, CancellationToken, Task<bool>> _waitForConfirmAsync;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly PeriodicTimer _timer;

    private readonly Dictionary<(DateOnly Day, TimeOnly BreakTime, string RoomId), DrawResult> _draws = new();
    private readonly HashSet<(DateOnly Day, TimeOnly BreakTime, RingStage Stage)> _fired = new();

    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    private enum RingStage { Draw, Recheck, Ring }

    public BreakScheduler(
        ConfigStore configStore,
        Func<IReadOnlySet<string>> getAvailableNodeIds,
        Func<bool> isSelfChief,
        Func<string, string, DateTimeOffset, CancellationToken, Task> sendRingCommandAsync,
        Func<string, TimeSpan, CancellationToken, Task<bool>> waitForConfirmAsync,
        TimeProvider? timeProvider = null,
        Random? random = null)
    {
        _configStore = configStore;
        _getAvailableNodeIds = getAvailableNodeIds;
        _isSelfChief = isSelfChief;
        _sendRingCommandAsync = sendRingCommandAsync;
        _waitForConfirmAsync = waitForConfirmAsync;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = random ?? Random.Shared;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(5), _timeProvider);
    }

    /// <summary>Raised after every draw, recheck, and ring resolution with the current
    /// per-room pick (null draw = nobody available / nobody confirmed) — lets the Chef publish
    /// a live preview so every node's UI can show who's up next without running the draw itself.</summary>
    public event Action<IReadOnlyList<(RoomGroup Room, DrawResult? Draw)>>? DrawUpdated;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loopTask = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(ct))
            {
                if (!_isSelfChief())
                {
                    continue; // only the Chef drives the timeline
                }

                var now = _timeProvider.GetLocalNow();
                var today = DateOnly.FromDateTime(now.DateTime);
                var nowTime = TimeOnly.FromDateTime(now.DateTime);

                foreach (var breakTime in _configStore.Current.Schedule.BreakTimes)
                {
                    var delta = breakTime.ToTimeSpan() - nowTime.ToTimeSpan();

                    if (ShouldFire(delta, DrawLeadTime) && _fired.Add((today, breakTime, RingStage.Draw)))
                    {
                        RunDraw(today, breakTime);
                    }

                    if (ShouldFire(delta, RecheckLeadTime) && _fired.Add((today, breakTime, RingStage.Recheck)))
                    {
                        RunRecheck(today, breakTime);
                    }

                    if (ShouldFire(delta, TimeSpan.Zero) && _fired.Add((today, breakTime, RingStage.Ring)))
                    {
                        // Fire-and-forget: walking the confirm/fallback chain can take several
                        // seconds and must not block the poll loop for other break times/rooms.
                        _ = RunRingAsync(today, breakTime, now, ct);
                    }
                }

                PruneStaleState(today);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool ShouldFire(TimeSpan delta, TimeSpan leadTime)
    {
        var untilLead = delta - leadTime;
        return untilLead <= TimeSpan.Zero && untilLead > -FireWindow;
    }

    private void RunDraw(DateOnly today, TimeOnly breakTime)
    {
        var available = _getAvailableNodeIds();
        var snapshot = new List<(RoomGroup Room, DrawResult? Draw)>();

        foreach (var room in _configStore.Current.Rooms.Rooms)
        {
            var draw = DrawService.Draw(room, available, _random);
            if (draw is not null)
            {
                _draws[(today, breakTime, room.RoomId)] = draw;
            }

            snapshot.Add((room, draw));
        }

        DrawUpdated?.Invoke(snapshot);
    }

    private void RunRecheck(DateOnly today, TimeOnly breakTime)
    {
        var available = _getAvailableNodeIds();
        var snapshot = new List<(RoomGroup Room, DrawResult? Draw)>();

        foreach (var room in _configStore.Current.Rooms.Rooms)
        {
            var key = (today, breakTime, room.RoomId);
            _draws.TryGetValue(key, out var existing);

            var resolved = existing;
            if (existing is null || !available.Contains(existing.DrawnNodeId))
            {
                var replacementId = existing?.CandidateOrder.FirstOrDefault(available.Contains);
                resolved = replacementId is not null
                    ? existing! with { DrawnNodeId = replacementId }
                    : DrawService.Draw(room, available, _random); // nobody from the original order is left — try fresh in case someone new came online
            }

            if (resolved is not null)
            {
                _draws[key] = resolved;
            }
            else
            {
                _draws.Remove(key);
            }

            snapshot.Add((room, resolved));
        }

        DrawUpdated?.Invoke(snapshot);
    }

    private async Task RunRingAsync(DateOnly today, TimeOnly breakTime, DateTimeOffset atTime, CancellationToken ct)
    {
        var outcome = new List<(RoomGroup Room, DrawResult? Draw)>();

        foreach (var room in _configStore.Current.Rooms.Rooms)
        {
            var key = (today, breakTime, room.RoomId);
            var draw = _draws.TryGetValue(key, out var d) ? d : DrawService.Draw(room, _getAvailableNodeIds(), _random);
            _draws.Remove(key);

            var resolvedNodeId = await RingWithFallbackAsync(room.Name, draw, atTime, ct);
            var resolvedDraw = draw is not null && resolvedNodeId is not null ? draw with { DrawnNodeId = resolvedNodeId } : null;
            outcome.Add((room, resolvedDraw));
        }

        DrawUpdated?.Invoke(outcome);
    }

    /// <summary>Sends the ring command to each candidate in order until one confirms in time,
    /// or the room's whole candidate order is exhausted.</summary>
    private async Task<string?> RingWithFallbackAsync(string roomName, DrawResult? draw, DateTimeOffset atTime, CancellationToken ct)
    {
        if (draw is null)
        {
            return null; // nobody was available in this room at all
        }

        foreach (var candidateId in draw.CandidateOrder)
        {
            await _sendRingCommandAsync(candidateId, roomName, atTime, ct);
            if (await _waitForConfirmAsync(candidateId, ConfirmTimeout, ct))
            {
                return candidateId;
            }
        }

        return null; // every candidate failed to confirm
    }

    private void PruneStaleState(DateOnly today)
    {
        _fired.RemoveWhere(k => k.Day != today);
        foreach (var key in _draws.Keys.Where(k => k.Day != today).ToList())
        {
            _draws.Remove(key);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _timer.Dispose();
        _cts?.Dispose();
    }
}
