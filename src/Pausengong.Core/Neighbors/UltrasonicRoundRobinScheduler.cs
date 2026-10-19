using Pausengong.Core.Models;

namespace Pausengong.Core.Neighbors;

/// <summary>
/// Computes the Track B round-robin schedule from the currently known online node ids. Pure and
/// stateless — see HANDOFF.md "Track B: ultrasonic presence sensing", step 2. Only the Chef calls
/// this (same "Chef is the sole scheduling authority" pattern as <see cref="Scheduling.BreakScheduler"/>),
/// but the resulting <see cref="UltrasonicRoundRobinSchedule"/> is broadcast so every node can
/// independently compute its own slot.
/// </summary>
public static class UltrasonicRoundRobinScheduler
{
    /// <summary>
    /// Order is deterministic (ordinal sort of node ids) rather than e.g. join order, so every
    /// node reconstructs the same schedule from the same inputs without needing to agree on
    /// anything beyond "who is currently known online" — same determinism reasoning as elsewhere
    /// in this codebase (e.g. succession ordering).
    /// </summary>
    public static UltrasonicRoundRobinSchedule BuildSchedule(
        IReadOnlyCollection<string> knownOnlineNodeIds,
        DateTimeOffset cycleStart,
        TimeSpan slotDuration)
    {
        var ordered = knownOnlineNodeIds
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        return new UltrasonicRoundRobinSchedule(ordered, cycleStart, slotDuration);
    }
}
