namespace Pausengong.Core.Config;

/// <summary>
/// <see cref="UpdatedAt"/> is this section's own version stamp — used for last-writer-wins
/// reconciliation when nodes exchange config over MQTT. Each of the four config sections
/// (schedule/rooms/room-mode/test-mode) versions independently, so editing one doesn't make
/// the others look newer than they are.
/// </summary>
public sealed record ScheduleConfig(IReadOnlyList<TimeOnly> BreakTimes, DateTimeOffset UpdatedAt)
{
    public static ScheduleConfig Empty { get; } = new(Array.Empty<TimeOnly>(), DateTimeOffset.MinValue);
}
