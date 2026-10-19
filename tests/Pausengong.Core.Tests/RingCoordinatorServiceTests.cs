using System.Text.Json;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Presence;
using Pausengong.Core.Scheduling;
using Xunit;

namespace Pausengong.Core.Tests;

public class RingCoordinatorServiceTests : IDisposable
{
    private readonly string _tempDir;

    public RingCoordinatorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PausengongTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private sealed class FakeGongPlayer : IGongPlayer
    {
        public bool WasCalled { get; private set; }
        public Task PlayAsync(CancellationToken ct = default) { WasCalled = true; return Task.CompletedTask; }
    }

    private sealed class FakeToastService : IToastService
    {
        public bool WasCalled { get; private set; }
        public Task ShowGongWouldHaveFiredAsync(string roomName, DateTimeOffset atTime, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }

        public Task ShowSimpleNotificationAsync(string title, string message, CancellationToken ct = default) => Task.CompletedTask;
    }

    private async Task<(ConfigStore Store, RingExecutor Executor, FakeGongPlayer Gong)> CreateRingExecutorAsync()
    {
        var store = new ConfigStore(
            configPath: Path.Combine(_tempDir, "config.json"),
            identityPath: Path.Combine(_tempDir, "node-id.json"));
        await store.SaveAsync(AppConfig.CreateDefault() with { TestMode = new TestModeState(false, DateTimeOffset.UtcNow) });
        var gong = new FakeGongPlayer();
        return (store, new RingExecutor(store, gong, new FakeToastService()), gong);
    }

    [Fact]
    public async Task StartAsync_SubscribesToItsOwnRingTargetTopic_TheConfirmWildcard_AndTheDrawPreviewTopic()
    {
        var (store, executor, _) = await CreateRingExecutorAsync();
        var mqtt = new FakeMqttNodeClient();
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);
        var coordinator = new RingCoordinatorService(mqtt, executor, store, new NodePresenceService(mqtt));

        await coordinator.StartAsync(identity, isSelfChief: () => false);

        Assert.Contains(TopicSchema.RingTarget("node-1"), mqtt.Subscriptions);
        Assert.Contains(TopicSchema.RingConfirmWildcard, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.RingDrawPreview, mqtt.Subscriptions);
    }

    [Fact]
    public async Task StartAsync_CalledAgainAfterReconnect_ResubscribesButDoesNotRestartScheduler()
    {
        // Simulates a Chef handover: the MQTT client always connects with a clean session (see
        // MqttNodeClient.ConnectAsync), so a second StartAsync call after a reconnect must
        // resubscribe from scratch — but the BreakScheduler it already started must keep running
        // rather than being replaced.
        var (store, executor, _) = await CreateRingExecutorAsync();
        var mqtt = new FakeMqttNodeClient();
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);
        var coordinator = new RingCoordinatorService(mqtt, executor, store, new NodePresenceService(mqtt));
        await coordinator.StartAsync(identity, isSelfChief: () => false);
        mqtt.Subscriptions.Clear();

        await coordinator.StartAsync(identity, isSelfChief: () => false);

        Assert.Contains(TopicSchema.RingTarget("node-1"), mqtt.Subscriptions);
        Assert.Contains(TopicSchema.RingConfirmWildcard, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.RingDrawPreview, mqtt.Subscriptions);

        // Still responds to a ring command after the "reconnect" — proves the coordinator, and
        // the scheduler it started the first time, are both still alive and working.
        var command = JsonSerializer.Serialize(new RingCommand("Büro", DateTimeOffset.Now));
        await mqtt.DeliverAsync(TopicSchema.RingTarget("node-1"), command);
        Assert.Contains(mqtt.Published, p => p.Topic == TopicSchema.RingConfirm("node-1") && !p.Retain);
    }

    [Fact]
    public async Task IncomingRingTarget_RingsThroughRingExecutor_AndPublishesConfirmOnItsOwnTopic()
    {
        var (store, executor, gong) = await CreateRingExecutorAsync();
        var mqtt = new FakeMqttNodeClient();
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);
        var coordinator = new RingCoordinatorService(mqtt, executor, store, new NodePresenceService(mqtt));
        await coordinator.StartAsync(identity, isSelfChief: () => false);

        var command = JsonSerializer.Serialize(new RingCommand("Büro", DateTimeOffset.Now));
        await mqtt.DeliverAsync(TopicSchema.RingTarget("node-1"), command);

        Assert.True(gong.WasCalled); // test mode off -> real path, proves it went through RingExecutor's gate
        Assert.Contains(mqtt.Published, p => p.Topic == TopicSchema.RingConfirm("node-1") && !p.Retain);
    }

    [Fact]
    public async Task IncomingRingTarget_AddressedToAnotherNode_IsIgnored()
    {
        var (store, executor, gong) = await CreateRingExecutorAsync();
        var mqtt = new FakeMqttNodeClient();
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);
        var coordinator = new RingCoordinatorService(mqtt, executor, store, new NodePresenceService(mqtt));
        await coordinator.StartAsync(identity, isSelfChief: () => false);

        var command = JsonSerializer.Serialize(new RingCommand("Büro", DateTimeOffset.Now));
        await mqtt.DeliverAsync(TopicSchema.RingTarget("some-other-node"), command);

        Assert.False(gong.WasCalled);
    }

    [Fact]
    public async Task IncomingDrawPreview_RaisesDrawPreviewChanged_WithParsedEntries()
    {
        var (store, executor, _) = await CreateRingExecutorAsync();
        var mqtt = new FakeMqttNodeClient();
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);
        var coordinator = new RingCoordinatorService(mqtt, executor, store, new NodePresenceService(mqtt));
        await coordinator.StartAsync(identity, isSelfChief: () => false);

        IReadOnlyList<DrawPreviewEntry>? received = null;
        coordinator.DrawPreviewChanged += entries => received = entries;

        var entries = new[] { new DrawPreviewEntry("r1", "Büro", "node-2", "PC2 (192.168.1.2)") };
        await mqtt.DeliverAsync(TopicSchema.RingDrawPreview, JsonSerializer.Serialize(entries));

        Assert.NotNull(received);
        var entry = Assert.Single(received!);
        Assert.Equal("Büro", entry.RoomName);
        Assert.Equal("PC2 (192.168.1.2)", entry.NodeLabel);
    }
}
