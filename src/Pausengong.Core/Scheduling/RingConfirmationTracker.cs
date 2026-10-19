namespace Pausengong.Core.Scheduling;

/// <summary>Tracks whether a node confirmed a ring command within a timeout window. One pending
/// wait per node at a time — that's all <see cref="BreakScheduler"/> ever needs, since it rings
/// its candidates one at a time per room.</summary>
public sealed class RingConfirmationTracker
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, TaskCompletionSource> _pending = new();

    public RingConfirmationTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Call when a <c>ring/confirm/{nodeId}</c> message arrives.</summary>
    public void OnConfirmReceived(string nodeId)
    {
        if (_pending.TryGetValue(nodeId, out var tcs))
        {
            tcs.TrySetResult();
        }
    }

    /// <summary>Waits up to <paramref name="timeout"/> for a confirm from <paramref name="nodeId"/>.</summary>
    public async Task<bool> WaitForConfirmAsync(string nodeId, TimeSpan timeout, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[nodeId] = tcs;
        try
        {
            var delayTask = Task.Delay(timeout, _timeProvider, ct);
            var completed = await Task.WhenAny(tcs.Task, delayTask);
            return completed == tcs.Task;
        }
        finally
        {
            _pending.Remove(nodeId);
        }
    }
}
