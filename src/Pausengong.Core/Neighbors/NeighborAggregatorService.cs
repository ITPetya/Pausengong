using System.Text.Json;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Presence;

namespace Pausengong.Core.Neighbors;

/// <summary>
/// MQTT glue shared by both room-detection tracks: subscribes to one track's neighbor-report
/// wildcard topic, feeds every arriving <see cref="NeighborReport"/> into
/// <see cref="NeighborGraphBuilder"/>/<see cref="ClusterCalculator"/>, and exposes the latest
/// clusters. One instance per track (BLE, ultrasonic) — register twice with that track's own
/// topic/threshold, never share an instance between tracks, since their signal units are not
/// comparable (see <see cref="NeighborEdge"/>).
///
/// Deliberately runs on every node, not just the Chef: computing clusters locally is harmless
/// redundancy (every node sees the same reports and gets the same answer). The actual
/// Chef-only-authority gating belongs one layer up, at the not-yet-built step that turns
/// <see cref="LatestClusters"/> into published <see cref="RoomGroup"/>s (see HANDOFF.md "Phase 7
/// replacement", step 6 / "Track B", step 6) — only the Chef should be the one publishing that,
/// same reasoning as <see cref="Scheduling.BreakScheduler"/>.
/// </summary>
public sealed class NeighborAggregatorService : IAsyncDisposable
{
    private readonly IMqttNodeClient _mqttClient;
    private readonly NodePresenceService _nodePresence;
    private readonly string _wildcardTopic;
    private readonly Func<string, string> _topicForNodeId;
    private readonly double _threshold;
    private readonly Dictionary<string, NeighborReport> _latestReportByObserver = new();

    public NeighborAggregatorService(
        IMqttNodeClient mqttClient,
        NodePresenceService nodePresence,
        string wildcardTopic,
        Func<string, string> topicForNodeId,
        double threshold)
    {
        _mqttClient = mqttClient;
        _nodePresence = nodePresence;
        _wildcardTopic = wildcardTopic;
        _topicForNodeId = topicForNodeId;
        _threshold = threshold;
        _mqttClient.MessageReceived += OnMessageReceivedAsync;
    }

    /// <summary>Raised whenever a new report changes the computed clusters.</summary>
    public event Action<IReadOnlyList<IReadOnlyList<string>>>? ClustersChanged;

    public IReadOnlyList<IReadOnlyList<string>> LatestClusters { get; private set; } = Array.Empty<IReadOnlyList<string>>();

    /// <summary>
    /// Call on every (re)connect, same reasoning as <see cref="Presence.NodePresenceService.OnConnectedAsync"/>:
    /// <see cref="MqttNodeClient.ConnectAsync"/> always connects with a clean session, so a
    /// reconnect starts with zero server-side subscriptions regardless of whether this service
    /// already ran once before.
    /// </summary>
    public Task OnConnectedAsync() => _mqttClient.SubscribeAsync(_wildcardTopic);

    private Task OnMessageReceivedAsync(string topic, string payload)
    {
        var nodeId = TopicSchema.ParseNodeIdFromNodesTopic(topic);
        if (nodeId is null || topic != _topicForNodeId(nodeId))
        {
            return Task.CompletedTask; // not this track's report topic
        }

        NeighborReport? report;
        try
        {
            report = JsonSerializer.Deserialize<NeighborReport>(payload);
        }
        catch (JsonException)
        {
            return Task.CompletedTask; // malformed/foreign payload — ignore
        }

        if (report is null)
        {
            return Task.CompletedTask;
        }

        _latestReportByObserver[report.ObserverNodeId] = report;
        Recompute();
        return Task.CompletedTask;
    }

    private void Recompute()
    {
        var allNodeIds = _nodePresence.KnownNodes.Select(n => n.NodeId).ToList();
        var edges = NeighborGraphBuilder.BuildEdges(_latestReportByObserver.Values.ToList());
        LatestClusters = ClusterCalculator.Cluster(allNodeIds, edges, _threshold);
        ClustersChanged?.Invoke(LatestClusters);
    }

    public ValueTask DisposeAsync()
    {
        _mqttClient.MessageReceived -= OnMessageReceivedAsync;
        return ValueTask.CompletedTask;
    }
}
