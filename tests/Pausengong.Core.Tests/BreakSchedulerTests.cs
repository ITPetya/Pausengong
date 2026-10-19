using Microsoft.Extensions.Time.Testing;
using Pausengong.Core.Config;
using Pausengong.Core.Models;
using Pausengong.Core.Scheduling;
using Xunit;

namespace Pausengong.Core.Tests;

public class BreakSchedulerTests : IDisposable
{
    private readonly string _tempDir;
    private static readonly TimeOnly BreakTime = new(9, 0);

    public BreakSchedulerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PausengongTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<ConfigStore> CreateStoreAsync(params RoomGroup[] rooms)
    {
        var store = new ConfigStore(
            configPath: Path.Combine(_tempDir, "config.json"),
            identityPath: Path.Combine(_tempDir, "node-id.json"));
        var config = AppConfig.CreateDefault() with
        {
            Schedule = new ScheduleConfig(new[] { BreakTime }, DateTimeOffset.UtcNow),
            Rooms = new RoomConfig(rooms, DateTimeOffset.UtcNow),
        };
        await store.SaveAsync(config);
        return store;
    }

    private static BreakScheduler CreateScheduler(
        ConfigStore store,
        FakeTimeProvider fakeTime,
        Func<IReadOnlySet<string>> getAvailable,
        Func<bool> isSelfChief,
        Func<string, string, DateTimeOffset, CancellationToken, Task>? sendRingCommandAsync = null,
        Func<string, TimeSpan, CancellationToken, Task<bool>>? waitForConfirmAsync = null) =>
        new(
            store,
            getAvailable,
            isSelfChief,
            sendRingCommandAsync ?? ((_, _, _, _) => Task.CompletedTask),
            waitForConfirmAsync ?? ((_, _, _) => Task.FromResult(true)),
            fakeTime,
            new Random(1));

    [Fact]
    public async Task Draw_FiresAtT60s_ForARoomWithAnAvailableMember()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 8, 58, 0, TimeSpan.Zero));
        var store = await CreateStoreAsync(new RoomGroup("r1", "Büro", new[] { "a", "b" }));
        var events = new List<IReadOnlyList<(RoomGroup Room, DrawResult? Draw)>>();

        using var scheduler = CreateScheduler(store, fakeTime, () => new HashSet<string> { "a", "b" }, () => true);
        scheduler.DrawUpdated += e => events.Add(e);
        scheduler.Start();

        fakeTime.Advance(TimeSpan.FromSeconds(60)); // now 08:59:00 — exactly T-60s
        await Task.Delay(200);

        var last = Assert.Single(events);
        var (room, draw) = Assert.Single(last);
        Assert.Equal("r1", room.RoomId);
        Assert.NotNull(draw);
        Assert.Contains(draw!.DrawnNodeId, new[] { "a", "b" });
    }

    [Fact]
    public async Task Recheck_ReplacesTheDrawnNode_WhenItWentOfflineBeforeT10s()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 8, 58, 0, TimeSpan.Zero));
        var store = await CreateStoreAsync(new RoomGroup("r1", "Büro", new[] { "a", "b" }));
        var available = new HashSet<string> { "a" }; // only "a" available at draw time -> deterministic pick
        var events = new List<IReadOnlyList<(RoomGroup Room, DrawResult? Draw)>>();

        using var scheduler = CreateScheduler(store, fakeTime, () => available, () => true);
        scheduler.DrawUpdated += e => events.Add(e);
        scheduler.Start();

        fakeTime.Advance(TimeSpan.FromSeconds(60)); // T-60s: draws "a" (only candidate)
        await Task.Delay(200);
        Assert.Equal("a", Assert.Single(events[0]).Draw!.DrawnNodeId);

        available.Clear();
        available.Add("b"); // "a" went offline, "b" came online

        fakeTime.Advance(TimeSpan.FromSeconds(50)); // now 08:59:50 — T-10s
        await Task.Delay(200);

        var recheck = events[^1];
        Assert.Equal("b", Assert.Single(recheck).Draw!.DrawnNodeId);
    }

    [Fact]
    public async Task Ring_ResolvesToTheConfirmingNode_AndSendsExactlyOneCommand()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 8, 58, 0, TimeSpan.Zero));
        var store = await CreateStoreAsync(new RoomGroup("r1", "Büro", new[] { "a" }));
        var sentTo = new List<string>();
        var events = new List<IReadOnlyList<(RoomGroup Room, DrawResult? Draw)>>();

        using var scheduler = CreateScheduler(
            store,
            fakeTime,
            () => new HashSet<string> { "a" },
            () => true,
            sendRingCommandAsync: (nodeId, _, _, _) => { sentTo.Add(nodeId); return Task.CompletedTask; },
            waitForConfirmAsync: (_, _, _) => Task.FromResult(true));
        scheduler.DrawUpdated += e => events.Add(e);
        scheduler.Start();

        fakeTime.Advance(TimeSpan.FromMinutes(1)); // T-60s
        await Task.Delay(200);
        fakeTime.Advance(TimeSpan.FromSeconds(50)); // T-10s
        await Task.Delay(200);
        fakeTime.Advance(TimeSpan.FromSeconds(10)); // T-0
        await Task.Delay(300);

        Assert.Equal(new[] { "a" }, sentTo);
        var final = events[^1];
        Assert.Equal("a", Assert.Single(final).Draw!.DrawnNodeId);
    }

    [Fact]
    public async Task Ring_FallsBackToNextCandidate_WhenTheFirstDoesNotConfirm()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 8, 58, 0, TimeSpan.Zero));
        var store = await CreateStoreAsync(new RoomGroup("r1", "Büro", new[] { "a", "b" }));
        var sentTo = new List<string>();
        var confirmCallCount = 0;

        using var scheduler = CreateScheduler(
            store,
            fakeTime,
            () => new HashSet<string> { "a", "b" },
            () => true,
            sendRingCommandAsync: (nodeId, _, _, _) => { sentTo.Add(nodeId); return Task.CompletedTask; },
            waitForConfirmAsync: (_, _, _) => Task.FromResult(++confirmCallCount >= 2)); // first candidate "times out", second confirms
        scheduler.Start();

        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(200);
        fakeTime.Advance(TimeSpan.FromSeconds(50));
        await Task.Delay(200);
        fakeTime.Advance(TimeSpan.FromSeconds(10));
        await Task.Delay(300);

        Assert.Equal(2, sentTo.Count);
        Assert.Equal(new[] { "a", "b" }, sentTo.OrderBy(x => x));
        Assert.NotEqual(sentTo[0], sentTo[1]);
    }

    [Fact]
    public async Task Nothing_Fires_WhenNotSelfChief()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 8, 58, 0, TimeSpan.Zero));
        var store = await CreateStoreAsync(new RoomGroup("r1", "Büro", new[] { "a" }));
        var sentTo = new List<string>();
        var events = new List<object>();

        using var scheduler = CreateScheduler(
            store,
            fakeTime,
            () => new HashSet<string> { "a" },
            () => false,
            sendRingCommandAsync: (nodeId, _, _, _) => { sentTo.Add(nodeId); return Task.CompletedTask; });
        scheduler.DrawUpdated += e => events.Add(e);
        scheduler.Start();

        fakeTime.Advance(TimeSpan.FromMinutes(2)); // well past every stage
        await Task.Delay(300);

        Assert.Empty(events);
        Assert.Empty(sentTo);
    }

    [Fact]
    public async Task Draw_ReturnsNullEntry_AndRingSendsNothing_WhenNoRoomMemberIsAvailable()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 8, 58, 0, TimeSpan.Zero));
        var store = await CreateStoreAsync(new RoomGroup("r1", "Büro", new[] { "a" }));
        var sentTo = new List<string>();
        var events = new List<IReadOnlyList<(RoomGroup Room, DrawResult? Draw)>>();

        using var scheduler = CreateScheduler(
            store,
            fakeTime,
            () => new HashSet<string>(), // nobody online
            () => true,
            sendRingCommandAsync: (nodeId, _, _, _) => { sentTo.Add(nodeId); return Task.CompletedTask; });
        scheduler.DrawUpdated += e => events.Add(e);
        scheduler.Start();

        fakeTime.Advance(TimeSpan.FromMinutes(1)); // T-60s
        await Task.Delay(200);

        Assert.Null(Assert.Single(events[0]).Draw);

        fakeTime.Advance(TimeSpan.FromSeconds(60)); // T-10s and T-0s
        await Task.Delay(300);

        Assert.Empty(sentTo);
    }
}
