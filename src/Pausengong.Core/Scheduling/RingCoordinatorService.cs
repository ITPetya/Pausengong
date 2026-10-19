using System.Text.Json;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Presence;

namespace Pausengong.Core.Scheduling;

/// <summary>
/// Wires the MQTT ring protocol to <see cref="BreakScheduler"/> and <see cref="RingExecutor"/>.
/// Every node subscribes to its own <c>ring/target</c> topic and rings (through the safety-gated
/// <see cref="RingExecutor"/>) whenever a command arrives there, then replies on
/// <c>ring/confirm</c> — that part runs the same way regardless of who's Chef, including on the
/// Chef itself (its own publish loops back through its own embedded broker). Only the Chef's
/// <see cref="BreakScheduler"/> actually drives the draw/ring timeline and sends commands.
/// </summary>
public sealed class RingCoordinatorService : IAsyncDisposable
{
    private readonly IMqttNodeClient _mqttClient;
    private readonly RingExecutor _ringExecutor;
    private readonly ConfigStore _configStore;
    private readonly NodePresenceService _nodePresence;
    private readonly RingConfirmationTracker _confirmationTracker = new();

    private NodeIdentity? _identity;
    private BreakScheduler? _scheduler;

    public RingCoordinatorService(IMqttNodeClient mqttClient, RingExecutor ringExecutor, ConfigStore configStore, NodePresenceService nodePresence)
    {
        _mqttClient = mqttClient;
        _ringExecutor = ringExecutor;
        _configStore = configStore;
        _nodePresence = nodePresence;
        _mqttClient.MessageReceived += OnMessageReceivedAsync;
    }

    /// <summary>Raised whenever the current per-room ring pick changes — received by every node
    /// (not just the Chef), so the UI can show who's up next without running the draw itself.</summary>
    public event Action<IReadOnlyList<DrawPreviewEntry>>? DrawPreviewChanged;

    /// <summary>
    /// Call every time this node (re)connects to a broker — e.g. alongside
    /// <c>ConfigSyncService.OnConnectedAsync</c>/<c>NodePresenceService.OnConnectedAsync</c> on
    /// <c>ChefElectionService.ChefChanged</c>. Re-subscribes every call, not just the first:
    /// <see cref="MqttNodeClient.ConnectAsync"/> always connects with a clean session, so a
    /// reconnect (e.g. after a Chef handover) starts with zero server-side subscriptions even
    /// though this service already ran once before. The <see cref="BreakScheduler"/> itself is
    /// still only started once — a reconnect doesn't need a new scheduling loop, just fresh
    /// subscriptions on top of the one already running.
    /// </summary>
    public async Task StartAsync(NodeIdentity identity, Func<bool> isSelfChief)
    {
        _identity = identity;

        await _mqttClient.SubscribeAsync(TopicSchema.RingTarget(identity.NodeId));
        await _mqttClient.SubscribeAsync(TopicSchema.RingConfirmWildcard);
        await _mqttClient.SubscribeAsync(TopicSchema.RingDrawPreview);

        if (_scheduler is not null)
        {
            return; // already running from an earlier connect
        }

        _scheduler = new BreakScheduler(
            _configStore,
            () => new HashSet<string>(_nodePresence.KnownNodes.Select(n => n.NodeId)),
            isSelfChief,
            SendRingCommandAsync,
            (nodeId, timeout, ct) => _confirmationTracker.WaitForConfirmAsync(nodeId, timeout, ct));
        _scheduler.DrawUpdated += OnDrawUpdated;
        _scheduler.Start();
    }

    private Task SendRingCommandAsync(string nodeId, string roomName, DateTimeOffset atTime, CancellationToken ct) =>
        _mqttClient.PublishAsync(TopicSchema.RingTarget(nodeId), JsonSerializer.Serialize(new RingCommand(roomName, atTime)), retain: false);

    private void OnDrawUpdated(IReadOnlyList<(RoomGroup Room, DrawResult? Draw)> draws)
    {
        var preview = draws
            .Where(d => d.Draw is not null)
            .Select(d => new DrawPreviewEntry(d.Room.RoomId, d.Room.Name, d.Draw!.DrawnNodeId, ResolveLabel(d.Draw.DrawnNodeId)))
            .ToList();

        DrawPreviewChanged?.Invoke(preview);
        _ = _mqttClient.PublishAsync(TopicSchema.RingDrawPreview, JsonSerializer.Serialize(preview), retain: false);
    }

    private string ResolveLabel(string nodeId) =>
        _nodePresence.KnownNodes.FirstOrDefault(n => n.NodeId == nodeId)?.DisplayLabel ?? nodeId;

    private async Task OnMessageReceivedAsync(string topic, string payload)
    {
        if (_identity is not null && topic == TopicSchema.RingTarget(_identity.NodeId))
        {
            await HandleRingTargetAsync(payload);
            return;
        }

        if (topic == TopicSchema.RingDrawPreview)
        {
            HandleDrawPreview(payload);
            return;
        }

        var confirmingNodeId = TopicSchema.ParseNodeIdFromRingConfirmTopic(topic);
        if (confirmingNodeId is not null)
        {
            _confirmationTracker.OnConfirmReceived(confirmingNodeId);
        }
    }

    private async Task HandleRingTargetAsync(string payload)
    {
        RingCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<RingCommand>(payload);
        }
        catch (JsonException)
        {
            return; // malformed/foreign payload — ignore
        }

        if (command is null || _identity is null)
        {
            return;
        }

        await _ringExecutor.ExecuteRingAsync(command.RoomName, command.AtTime);
        await _mqttClient.PublishAsync(TopicSchema.RingConfirm(_identity.NodeId), "confirmed", retain: false);
    }

    private void HandleDrawPreview(string payload)
    {
        List<DrawPreviewEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<DrawPreviewEntry>>(payload);
        }
        catch (JsonException)
        {
            return;
        }

        if (entries is not null)
        {
            DrawPreviewChanged?.Invoke(entries);
        }
    }

    public ValueTask DisposeAsync()
    {
        _mqttClient.MessageReceived -= OnMessageReceivedAsync;
        if (_scheduler is not null)
        {
            _scheduler.DrawUpdated -= OnDrawUpdated;
            _scheduler.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
