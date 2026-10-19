using System.Text.Json;
using Pausengong.Core.Config;
using Pausengong.Core.Election;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;

namespace Pausengong.Core.Presence;

/// <summary>
/// Tracks which nodes are currently reachable on the network so the UI can list them for room
/// assignment. Every node publishes its own identity as a retained message on every (re)connect
/// — mirroring how <see cref="Mqtt.MqttNodeClient.ConnectAsync"/> already republishes its
/// "online" LWT topic on every connect — because the embedded broker is ephemeral and dies with
/// the Chef, so retained history alone doesn't survive a handover.
///
/// Tolerant of peers still running an older build that only publishes the plain "online" LWT
/// topic (no JSON status): those are tracked under their bare node id so they still show up in
/// the list without requiring every machine on the network to update in lockstep.
/// </summary>
public sealed class NodePresenceService
{
    private readonly IMqttNodeClient _mqttClient;
    private readonly Dictionary<string, NodeInfo> _nodes = new();
    private string? _selfNodeId;

    public NodePresenceService(IMqttNodeClient mqttClient)
    {
        _mqttClient = mqttClient;
        _mqttClient.MessageReceived += OnMessageReceivedAsync;
    }

    /// <summary>Raised whenever a node is added, updated, or drops offline.</summary>
    public event Action? NodesChanged;

    public IReadOnlyCollection<NodeInfo> KnownNodes => _nodes.Values.ToList();

    /// <summary>
    /// Call whenever this node (re)connects to a broker — e.g. on <c>ChefElectionService.ChefChanged</c>.
    /// Re-subscribes every time, not just on the first call: <see cref="MqttNodeClient.ConnectAsync"/>
    /// always connects with a clean session, so a reconnect (e.g. after a Chef handover) starts with
    /// zero server-side subscriptions even though this service already ran once before.
    /// </summary>
    public async Task OnConnectedAsync(NodeIdentity self)
    {
        _selfNodeId = self.NodeId;

        await _mqttClient.SubscribeAsync(TopicSchema.NodeStatusWildcard);
        await _mqttClient.SubscribeAsync(TopicSchema.NodeOnlineWildcard);

        var selfInfo = new NodeInfo(self.NodeId, self.HostName, self.CreatedAt) { Address = NetworkHelper.GetPrimaryIPv4Address() };
        UpsertLocally(selfInfo);

        await _mqttClient.PublishAsync(TopicSchema.NodeStatus(self.NodeId), JsonSerializer.Serialize(selfInfo), retain: true);
    }

    private Task OnMessageReceivedAsync(string topic, string payload)
    {
        var nodeId = TopicSchema.ParseNodeIdFromNodesTopic(topic);
        if (nodeId is null || nodeId == _selfNodeId)
        {
            return Task.CompletedTask; // not a nodes/ topic, or our own echo — nothing to do
        }

        if (topic == TopicSchema.NodeOnline(nodeId))
        {
            HandleOnlineTopic(nodeId, payload);
            return Task.CompletedTask;
        }

        if (topic == TopicSchema.NodeStatus(nodeId))
        {
            HandleStatusTopic(nodeId, payload);
        }

        return Task.CompletedTask;
    }

    private void HandleOnlineTopic(string nodeId, string payload)
    {
        if (payload == "offline")
        {
            if (_nodes.Remove(nodeId))
            {
                NodesChanged?.Invoke();
            }

            return;
        }

        // "online" from a peer we don't have a JSON status for yet (older build, or the status
        // message just hasn't arrived) — track it under its bare id so it's still listed.
        if (!_nodes.ContainsKey(nodeId))
        {
            UpsertLocally(new NodeInfo(nodeId, nodeId, DateTimeOffset.UtcNow));
        }
    }

    private void HandleStatusTopic(string nodeId, string payload)
    {
        NodeInfo? incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<NodeInfo>(payload);
        }
        catch (JsonException)
        {
            return; // malformed/foreign payload — ignore
        }

        if (incoming is null)
        {
            return;
        }

        UpsertLocally(incoming);
    }

    private void UpsertLocally(NodeInfo node)
    {
        _nodes[node.NodeId] = node with { LastSeen = DateTimeOffset.UtcNow };
        NodesChanged?.Invoke();
    }
}
