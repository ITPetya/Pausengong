using System.Text.Json;
using Pausengong.Core.Models;
using Pausengong.Core.Mqtt;

namespace Pausengong.Core.Config;

/// <summary>
/// Keeps <see cref="ConfigStore"/> in sync across the network over MQTT.
///
/// Each of the four config sections is its own retained topic carrying its own
/// <c>UpdatedAt</c> stamp; an incoming section is only adopted if it's strictly newer than
/// what's stored locally (last-writer-wins), so this is safe against receiving stale or
/// duplicate (including our own echoed) messages.
///
/// Retained messages alone aren't enough here: the embedded MQTT broker is ephemeral — it
/// runs in-process on whichever PC is currently Chef, so a Chef handover means a brand new
/// broker with no retained history. To cover that gap, every (re)connect also publishes a
/// non-retained <see cref="TopicSchema.ConfigRequest"/> signal; every node that receives it
/// (including, harmlessly, the requester's own echo) republishes its current known config, so
/// the network converges again even after a broker restart.
/// </summary>
public sealed class ConfigSyncService
{
    private readonly ConfigStore _configStore;
    private readonly IMqttNodeClient _mqttClient;

    public ConfigSyncService(ConfigStore configStore, IMqttNodeClient mqttClient)
    {
        _configStore = configStore;
        _mqttClient = mqttClient;
        _mqttClient.MessageReceived += OnMessageReceivedAsync;
    }

    /// <summary>
    /// Call whenever this node (re)connects to a broker — e.g. on <c>ChefElectionService.ChefChanged</c>.
    /// Re-subscribes every time, not just on the first call: <see cref="MqttNodeClient.ConnectAsync"/>
    /// always connects with a clean session, so a reconnect (e.g. after a Chef handover) starts with
    /// zero server-side subscriptions even though this service already ran once before.
    /// </summary>
    public async Task OnConnectedAsync()
    {
        await _mqttClient.SubscribeAsync(TopicSchema.ConfigSchedule);
        await _mqttClient.SubscribeAsync(TopicSchema.ConfigRooms);
        await _mqttClient.SubscribeAsync(TopicSchema.ConfigRoomMode);
        await _mqttClient.SubscribeAsync(TopicSchema.ConfigTestMode);
        await _mqttClient.SubscribeAsync(TopicSchema.ConfigRequest);

        await _mqttClient.PublishAsync(TopicSchema.ConfigRequest, payload: "?", retain: false);
    }

    public Task UpdateScheduleAsync(IReadOnlyList<TimeOnly> breakTimes) =>
        ApplyLocalAndPublishAsync(_configStore.Current with { Schedule = new ScheduleConfig(breakTimes, DateTimeOffset.UtcNow) });

    public Task UpdateRoomsAsync(IReadOnlyList<RoomGroup> rooms) =>
        ApplyLocalAndPublishAsync(_configStore.Current with { Rooms = new RoomConfig(rooms, DateTimeOffset.UtcNow) });

    public Task UpdateRoomModeAsync(RoomMode mode) =>
        ApplyLocalAndPublishAsync(_configStore.Current with { RoomMode = new RoomModeConfig(mode, DateTimeOffset.UtcNow) });

    public Task UpdateTestModeAsync(bool isActive) =>
        ApplyLocalAndPublishAsync(_configStore.Current with { TestMode = new TestModeState(isActive, DateTimeOffset.UtcNow) });

    private async Task ApplyLocalAndPublishAsync(AppConfig updated)
    {
        var previous = _configStore.Current;
        await _configStore.SaveAsync(updated);

        if (!ReferenceEquals(previous.Schedule, updated.Schedule) || previous.Schedule.UpdatedAt != updated.Schedule.UpdatedAt)
        {
            await PublishScheduleAsync(updated.Schedule);
        }

        if (!ReferenceEquals(previous.Rooms, updated.Rooms) || previous.Rooms.UpdatedAt != updated.Rooms.UpdatedAt)
        {
            await PublishRoomsAsync(updated.Rooms);
        }

        if (!ReferenceEquals(previous.RoomMode, updated.RoomMode) || previous.RoomMode.UpdatedAt != updated.RoomMode.UpdatedAt)
        {
            await PublishRoomModeAsync(updated.RoomMode);
        }

        if (!ReferenceEquals(previous.TestMode, updated.TestMode) || previous.TestMode.UpdatedAt != updated.TestMode.UpdatedAt)
        {
            await PublishTestModeAsync(updated.TestMode);
        }
    }

    private Task PublishScheduleAsync(ScheduleConfig config) =>
        _mqttClient.PublishAsync(TopicSchema.ConfigSchedule, JsonSerializer.Serialize(config), retain: true);

    private Task PublishRoomsAsync(RoomConfig config) =>
        _mqttClient.PublishAsync(TopicSchema.ConfigRooms, JsonSerializer.Serialize(config), retain: true);

    private Task PublishRoomModeAsync(RoomModeConfig config) =>
        _mqttClient.PublishAsync(TopicSchema.ConfigRoomMode, JsonSerializer.Serialize(config), retain: true);

    private Task PublishTestModeAsync(TestModeState config) =>
        _mqttClient.PublishAsync(TopicSchema.ConfigTestMode, JsonSerializer.Serialize(config), retain: true);

    private async Task OnMessageReceivedAsync(string topic, string payload)
    {
        switch (topic)
        {
            case TopicSchema.ConfigSchedule:
                await TryAdoptAsync(payload, c => c.Schedule, (cfg, s) => cfg with { Schedule = s });
                break;
            case TopicSchema.ConfigRooms:
                await TryAdoptAsync(payload, c => c.Rooms, (cfg, r) => cfg with { Rooms = r });
                break;
            case TopicSchema.ConfigRoomMode:
                await TryAdoptAsync(payload, c => c.RoomMode, (cfg, m) => cfg with { RoomMode = m });
                break;
            case TopicSchema.ConfigTestMode:
                await TryAdoptAsync(payload, c => c.TestMode, (cfg, t) => cfg with { TestMode = t });
                break;
            case TopicSchema.ConfigRequest:
                var current = _configStore.Current;
                await PublishScheduleAsync(current.Schedule);
                await PublishRoomsAsync(current.Rooms);
                await PublishRoomModeAsync(current.RoomMode);
                await PublishTestModeAsync(current.TestMode);
                break;
        }
    }

    private async Task TryAdoptAsync<TSection>(
        string payload,
        Func<AppConfig, TSection> selectLocal,
        Func<AppConfig, TSection, AppConfig> withSection)
        where TSection : class
    {
        TSection? incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<TSection>(payload);
        }
        catch (JsonException)
        {
            return; // malformed payload from an incompatible/foreign publisher — ignore, don't crash the sync loop
        }

        if (incoming is null)
        {
            return;
        }

        var localUpdatedAt = GetUpdatedAt(selectLocal(_configStore.Current));
        var incomingUpdatedAt = GetUpdatedAt(incoming);
        if (incomingUpdatedAt <= localUpdatedAt)
        {
            return; // not newer than what we already have (including our own echo) — ignore
        }

        await _configStore.SaveAsync(withSection(_configStore.Current, incoming));
    }

    private static DateTimeOffset GetUpdatedAt(object section) => section switch
    {
        ScheduleConfig s => s.UpdatedAt,
        RoomConfig r => r.UpdatedAt,
        RoomModeConfig m => m.UpdatedAt,
        TestModeState t => t.UpdatedAt,
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };
}
