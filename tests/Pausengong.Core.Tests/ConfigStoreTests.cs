using Pausengong.Core.Config;
using Pausengong.Core.Models;
using Xunit;

namespace Pausengong.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PausengongTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private ConfigStore CreateStore() => new(
        configPath: Path.Combine(_tempDir, "config.json"),
        identityPath: Path.Combine(_tempDir, "node-id.json"));

    [Fact]
    public async Task LoadAsync_WhenNoFileExists_ReturnsDefaultWithTestModeOnAndOneDefaultRoom()
    {
        var store = CreateStore();

        var config = await store.LoadAsync();

        Assert.True(config.TestMode.IsActive);
        Assert.Empty(config.Schedule.BreakTimes);
        var room = Assert.Single(config.Rooms.Rooms);
        Assert.Equal(RoomConfig.DefaultRoomId, room.RoomId);
        Assert.Equal(RoomMode.Manual, config.RoomMode.Mode);
    }

    [Fact]
    public async Task LoadAsync_SelfHealsAnExistingConfigWithZeroRooms_ByAddingTheDefaultRoomInMemory()
    {
        // Reproduces a real report: an install saved before the default room existed had
        // Rooms = [] on disk, so BreakScheduler never had anything to draw against even with a
        // schedule configured and exactly one PC online.
        var store = CreateStore();
        await store.LoadAsync();
        await store.SaveAsync(AppConfig.CreateDefault() with { Rooms = new RoomConfig(Array.Empty<RoomGroup>(), DateTimeOffset.MinValue) });

        var reloaded = await CreateStore().LoadAsync();

        var room = Assert.Single(reloaded.Rooms.Rooms);
        Assert.Equal(RoomConfig.DefaultRoomId, room.RoomId);
        // Must not fabricate a fresh timestamp for the healed section — that could make it look
        // newer than a peer's real room setup during MQTT last-writer-wins sync.
        Assert.Equal(DateTimeOffset.MinValue, reloaded.Rooms.UpdatedAt);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllFields()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var config = new AppConfig(
            new ScheduleConfig(new[] { new TimeOnly(9, 0), new TimeOnly(12, 29) }, now),
            new RoomConfig(new[] { new RoomGroup("room-1", "Klassenzimmer 3B", new[] { "pc-a", "pc-b" }) }, now),
            new RoomModeConfig(RoomMode.Automatic, now),
            new TestModeState(IsActive: false, now));

        await store.SaveAsync(config);

        var reloaded = await CreateStore().LoadAsync();
        Assert.Equal(config.Schedule.BreakTimes, reloaded.Schedule.BreakTimes);
        Assert.Equal("room-1", reloaded.Rooms.Rooms[0].RoomId);
        Assert.Equal(RoomMode.Automatic, reloaded.RoomMode.Mode);
        Assert.False(reloaded.TestMode.IsActive);
    }

    [Fact]
    public async Task SaveAsync_RaisesConfigChanged()
    {
        var store = CreateStore();
        AppConfig? observed = null;
        store.ConfigChanged += cfg => observed = cfg;

        var config = AppConfig.CreateDefault() with { TestMode = new TestModeState(false, DateTimeOffset.UtcNow) };
        await store.SaveAsync(config);

        Assert.NotNull(observed);
        Assert.False(observed!.TestMode.IsActive);
    }

    [Fact]
    public async Task LoadOrCreateIdentityAsync_PersistsAndReturnsSameIdentityAcrossCalls()
    {
        var store = CreateStore();

        var first = await store.LoadOrCreateIdentityAsync();
        var second = await CreateStore().LoadOrCreateIdentityAsync();

        Assert.Equal(first.NodeId, second.NodeId);
        Assert.Equal(first.HostName, second.HostName);
    }
}
