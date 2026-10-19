using System.Text.Json;
using Pausengong.Core.Config;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;
using Xunit;

namespace Pausengong.Core.Tests;

public class ConfigSyncServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigSyncServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PausengongTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private ConfigStore CreateStore() => new(
        configPath: Path.Combine(_tempDir, "config.json"),
        identityPath: Path.Combine(_tempDir, "node-id.json"));

    [Fact]
    public async Task UpdateScheduleAsync_PublishesOnlyScheduleTopic_NotOtherSections()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        await sync.UpdateScheduleAsync(new[] { new TimeOnly(9, 0) });

        var published = Assert.Single(mqtt.Published);
        Assert.Equal(TopicSchema.ConfigSchedule, published.Topic);
        Assert.True(published.Retain);
    }

    [Fact]
    public async Task UpdateTestModeAsync_PublishesOnlyTestModeTopic()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        await sync.UpdateTestModeAsync(false);

        var published = Assert.Single(mqtt.Published);
        Assert.Equal(TopicSchema.ConfigTestMode, published.Topic);
    }

    [Fact]
    public async Task IncomingMessage_WithNewerTimestamp_IsAdoptedLocally()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        var incoming = new ScheduleConfig(new[] { new TimeOnly(12, 0) }, DateTimeOffset.UtcNow.AddMinutes(5));
        await mqtt.DeliverAsync(TopicSchema.ConfigSchedule, JsonSerializer.Serialize(incoming));

        Assert.Equal(new TimeOnly(12, 0), Assert.Single(store.Current.Schedule.BreakTimes));
    }

    [Fact]
    public async Task IncomingMessage_WithOlderOrEqualTimestamp_IsIgnored()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        // Establish a local value with a recent timestamp first.
        await sync.UpdateScheduleAsync(new[] { new TimeOnly(9, 0) });
        var localBefore = store.Current.Schedule;

        // An older update (e.g. our own stale echo, or a peer with outdated data) must not win.
        var stale = new ScheduleConfig(new[] { new TimeOnly(15, 0) }, localBefore.UpdatedAt.AddMinutes(-1));
        await mqtt.DeliverAsync(TopicSchema.ConfigSchedule, JsonSerializer.Serialize(stale));

        Assert.Equal(localBefore.BreakTimes, store.Current.Schedule.BreakTimes);
    }

    [Fact]
    public async Task ReceivingConfigRequest_RepublishesAllFourSections()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        await mqtt.DeliverAsync(TopicSchema.ConfigRequest, "?");

        var topics = mqtt.Published.Select(p => p.Topic).ToHashSet();
        Assert.Equal(
            new[] { TopicSchema.ConfigSchedule, TopicSchema.ConfigRooms, TopicSchema.ConfigRoomMode, TopicSchema.ConfigTestMode }.ToHashSet(),
            topics);
    }

    [Fact]
    public async Task OnConnectedAsync_SubscribesToAllConfigTopics_AndPublishesRequest()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        await sync.OnConnectedAsync();

        Assert.Contains(TopicSchema.ConfigSchedule, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigRooms, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigRoomMode, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigTestMode, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigRequest, mqtt.Subscriptions);
        Assert.Contains(mqtt.Published, p => p.Topic == TopicSchema.ConfigRequest && !p.Retain);
    }

    [Fact]
    public async Task OnConnectedAsync_CalledAgainAfterReconnect_ResubscribesToEveryTopic()
    {
        // Simulates a Chef handover: the MQTT client always connects with a clean session (see
        // MqttNodeClient.ConnectAsync), so a second OnConnectedAsync call after a reconnect must
        // resubscribe from scratch rather than assume the first call's subscriptions still hold.
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        await sync.OnConnectedAsync();
        mqtt.Subscriptions.Clear();

        await sync.OnConnectedAsync();

        Assert.Contains(TopicSchema.ConfigSchedule, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigRooms, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigRoomMode, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigTestMode, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.ConfigRequest, mqtt.Subscriptions);
    }

    [Fact]
    public async Task UpdateRoomsAsync_DoesNotRepublish_UnrelatedSections()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var mqtt = new FakeMqttNodeClient();
        var sync = new ConfigSyncService(store, mqtt);

        // Prime schedule with a value first.
        await sync.UpdateScheduleAsync(new[] { new TimeOnly(9, 0) });
        mqtt.Published.Clear();

        await sync.UpdateRoomsAsync(new[] { new RoomGroup("room-1", "Büro", new[] { "pc-a" }) });

        var published = Assert.Single(mqtt.Published);
        Assert.Equal(TopicSchema.ConfigRooms, published.Topic);
    }
}
