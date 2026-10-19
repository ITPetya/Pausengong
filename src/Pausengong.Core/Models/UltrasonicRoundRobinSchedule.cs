namespace Pausengong.Core.Models;

/// <summary>
/// A Chef-computed, Chef-published time-division schedule for Track B (ultrasonic presence
/// sensing) — see HANDOFF.md "Track B: ultrasonic presence sensing", step 2 (collision handling).
/// Exactly one node transmits the shared tone at a time, in a fixed order, so multiple PCs in the
/// same room never chirp simultaneously and corrupt each other's amplitude reading. Published
/// non-retained on <see cref="Mqtt.TopicSchema.UltrasonicSchedule"/>; every node (not just the
/// Chef) uses it to know when it is its own turn to play the tone vs. everyone else's turn to
/// listen.
/// </summary>
public sealed record UltrasonicRoundRobinSchedule(
    IReadOnlyList<string> OrderedNodeIds,
    DateTimeOffset CycleStart,
    TimeSpan SlotDuration)
{
    public TimeSpan CycleDuration => SlotDuration * OrderedNodeIds.Count;

    /// <summary>Which node should be transmitting at <paramref name="instant"/>, or
    /// <c>null</c> if that instant falls outside this cycle entirely (before <see cref="CycleStart"/>
    /// or at/after <see cref="CycleStart"/> + <see cref="CycleDuration"/>).</summary>
    public string? TransmittingNodeIdAt(DateTimeOffset instant)
    {
        if (OrderedNodeIds.Count == 0)
        {
            return null;
        }

        var elapsed = instant - CycleStart;
        if (elapsed < TimeSpan.Zero || elapsed >= CycleDuration)
        {
            return null;
        }

        var slotIndex = (int)(elapsed.Ticks / SlotDuration.Ticks);
        return OrderedNodeIds[slotIndex];
    }

    /// <summary>When <paramref name="nodeId"/>'s own slot starts, or <c>null</c> if it is not
    /// part of this schedule (e.g. it joined after the Chef computed it — it just waits for the
    /// next cycle).</summary>
    public DateTimeOffset? SlotStartFor(string nodeId)
    {
        for (var index = 0; index < OrderedNodeIds.Count; index++)
        {
            if (OrderedNodeIds[index] == nodeId)
            {
                return CycleStart + (SlotDuration * index);
            }
        }

        return null;
    }
}
