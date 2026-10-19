using System.Text.Json;
using Pausengong.Core.Config;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Neighbors;
using Pausengong.Core.Presence;
using Xunit;

namespace Pausengong.Core.Tests;

public class NeighborAggregatorServiceTests
{
    private static async Task<(FakeMqttNodeClient Mqtt, NodePresenceService Presence)> SetUpThreeKnownNodesAsync()
    {
        var mqtt = new FakeMqttNodeClient();
        var presence = new NodePresenceService(mqtt);
        await presence.OnConnectedAsync(new NodeIdentity("a", "PC-A", DateTimeOffset.UtcNow));
        await mqtt.DeliverAsync(TopicSchema.NodeStatus("b"), JsonSerializer.Serialize(new NodeInfo("b", "PC-B", DateTimeOffset.UtcNow)));
        await mqtt.DeliverAsync(TopicSchema.NodeStatus("c"), JsonSerializer.Serialize(new NodeInfo("c", "PC-C", DateTimeOffset.UtcNow)));
        return (mqtt, presence);
    }

    [Fact]
    public async Task OnConnectedAsync_SubscribesToTheGivenWildcardTopic()
    {
        var (mqtt, presence) = await SetUpThreeKnownNodesAsync();
        var aggregator = new NeighborAggregatorService(mqtt, presence, TopicSchema.NodeBleNeighborsWildcard, TopicSchema.NodeBleNeighbors, threshold: -70);

        await aggregator.OnConnectedAsync();

        Assert.Contains(TopicSchema.NodeBleNeighborsWildcard, mqtt.Subscriptions);
    }

    [Fact]
    public async Task IncomingReport_OnItsOwnTrackTopic_UpdatesClustersAndRaisesEvent()
    {
        var (mqtt, presence) = await SetUpThreeKnownNodesAsync();
        var aggregator = new NeighborAggregatorService(mqtt, presence, TopicSchema.NodeBleNeighborsWildcard, TopicSchema.NodeBleNeighbors, threshold: -70);
        IReadOnlyList<IReadOnlyList<string>>? raised = null;
        aggregator.ClustersChanged += clusters => raised = clusters;

        var report = new NeighborReport("a", new Dictionary<string, double> { ["b"] = -60 }, DateTimeOffset.UtcNow);
        await mqtt.DeliverAsync(TopicSchema.NodeBleNeighbors("a"), JsonSerializer.Serialize(report));

        Assert.NotNull(raised);
        Assert.Contains(raised!, c => c.OrderBy(x => x).SequenceEqual(new[] { "a", "b" }));
        Assert.Contains(aggregator.LatestClusters, c => c.OrderBy(x => x).SequenceEqual(new[] { "a", "b" }));
        Assert.Contains(aggregator.LatestClusters, c => c.SequenceEqual(new[] { "c" })); // no edge reported for c -> its own singleton
    }

    [Fact]
    public async Task IncomingReport_OnTheOtherTracksTopic_IsIgnored()
    {
        // Same wildcard shape (pausengong/nodes/{id}/...), different suffix — must not leak
        // across tracks even though TopicSchema.ParseNodeIdFromNodesTopic would happily extract
        // a node id from either.
        var (mqtt, presence) = await SetUpThreeKnownNodesAsync();
        var bleAggregator = new NeighborAggregatorService(mqtt, presence, TopicSchema.NodeBleNeighborsWildcard, TopicSchema.NodeBleNeighbors, threshold: -70);
        var raisedCount = 0;
        bleAggregator.ClustersChanged += _ => raisedCount++;

        var ultrasonicReport = new NeighborReport("a", new Dictionary<string, double> { ["b"] = 0.9 }, DateTimeOffset.UtcNow);
        await mqtt.DeliverAsync(TopicSchema.NodeUltrasonicNeighbors("a"), JsonSerializer.Serialize(ultrasonicReport));

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public async Task MalformedPayload_IsIgnoredWithoutThrowing()
    {
        var (mqtt, presence) = await SetUpThreeKnownNodesAsync();
        var aggregator = new NeighborAggregatorService(mqtt, presence, TopicSchema.NodeBleNeighborsWildcard, TopicSchema.NodeBleNeighbors, threshold: -70);

        await mqtt.DeliverAsync(TopicSchema.NodeBleNeighbors("a"), "{ not valid json");

        Assert.Empty(aggregator.LatestClusters);
    }

    [Fact]
    public async Task TwoTrackInstances_SharingTheSameMqttClient_StayIndependent()
    {
        var (mqtt, presence) = await SetUpThreeKnownNodesAsync();
        var bleAggregator = new NeighborAggregatorService(mqtt, presence, TopicSchema.NodeBleNeighborsWildcard, TopicSchema.NodeBleNeighbors, threshold: -70);
        var ultrasonicAggregator = new NeighborAggregatorService(mqtt, presence, TopicSchema.NodeUltrasonicNeighborsWildcard, TopicSchema.NodeUltrasonicNeighbors, threshold: 0.5);

        await mqtt.DeliverAsync(TopicSchema.NodeBleNeighbors("a"), JsonSerializer.Serialize(
            new NeighborReport("a", new Dictionary<string, double> { ["b"] = -60 }, DateTimeOffset.UtcNow)));
        await mqtt.DeliverAsync(TopicSchema.NodeUltrasonicNeighbors("b"), JsonSerializer.Serialize(
            new NeighborReport("b", new Dictionary<string, double> { ["c"] = 0.9 }, DateTimeOffset.UtcNow)));

        Assert.Contains(bleAggregator.LatestClusters, c => c.OrderBy(x => x).SequenceEqual(new[] { "a", "b" }));
        Assert.Contains(ultrasonicAggregator.LatestClusters, c => c.OrderBy(x => x).SequenceEqual(new[] { "b", "c" }));
    }
}
