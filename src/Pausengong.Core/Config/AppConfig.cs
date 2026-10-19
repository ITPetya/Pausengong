namespace Pausengong.Core.Config;

/// <summary>
/// The full set of network-wide config that must be identical on every node. Persisted
/// locally as one file; each section carries its own <c>UpdatedAt</c> stamp used for
/// last-writer-wins reconciliation when nodes exchange config over MQTT (see
/// <see cref="ConfigSyncService"/>) — there's no single version for the whole record since
/// sections change independently.
/// </summary>
public sealed record AppConfig(
    ScheduleConfig Schedule,
    RoomConfig Rooms,
    RoomModeConfig RoomMode,
    TestModeState TestMode)
{
    public static AppConfig CreateDefault() => new(
        ScheduleConfig.Empty,
        RoomConfig.Default,
        RoomModeConfig.Default,
        TestModeState.Default);
}
