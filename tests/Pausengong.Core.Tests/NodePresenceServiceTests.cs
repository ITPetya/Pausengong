using Pausengong.Core.Config;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Presence;
using Xunit;

namespace Pausengong.Core.Tests;

public class NodePresenceServiceTests
{
    [Fact]
    public async Task OnConnectedAsync_SubscribesToNodeStatusAndOnlineWildcards_AndPublishesRetainedSelfStatus()
    {
        var mqtt = new FakeMqttNodeClient();
        var presence = new NodePresenceService(mqtt);
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);

        await presence.OnConnectedAsync(identity);

        Assert.Contains(TopicSchema.NodeStatusWildcard, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.NodeOnlineWildcard, mqtt.Subscriptions);
        Assert.Contains(mqtt.Published, p => p.Topic == TopicSchema.NodeStatus("node-1") && p.Retain);
        Assert.Contains(presence.KnownNodes, n => n.NodeId == "node-1");
    }

    [Fact]
    public async Task OnConnectedAsync_CalledAgainAfterReconnect_ResubscribesToBothWildcards()
    {
        // Simulates a Chef handover: the MQTT client always connects with a clean session (see
        // MqttNodeClient.ConnectAsync), so a second OnConnectedAsync call after a reconnect must
        // resubscribe from scratch rather than assume the first call's subscriptions still hold.
        var mqtt = new FakeMqttNodeClient();
        var presence = new NodePresenceService(mqtt);
        var identity = new NodeIdentity("node-1", "PC1", DateTimeOffset.UtcNow);
        await presence.OnConnectedAsync(identity);
        mqtt.Subscriptions.Clear();

        await presence.OnConnectedAsync(identity);

        Assert.Contains(TopicSchema.NodeStatusWildcard, mqtt.Subscriptions);
        Assert.Contains(TopicSchema.NodeOnlineWildcard, mqtt.Subscriptions);
    }
}
