using Pausengong.Core.Neighbors;
using Xunit;

namespace Pausengong.Core.Tests;

public class UltrasonicRoundRobinSchedulerTests
{
    private static readonly DateTimeOffset CycleStart = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan SlotDuration = TimeSpan.FromSeconds(2);

    [Fact]
    public void BuildSchedule_OrdersNodesDeterministically_RegardlessOfInputOrder()
    {
        var scheduleA = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "c", "a", "b" }, CycleStart, SlotDuration);
        var scheduleB = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "b", "c", "a" }, CycleStart, SlotDuration);

        Assert.Equal(scheduleA.OrderedNodeIds, scheduleB.OrderedNodeIds);
        Assert.Equal(new[] { "a", "b", "c" }, scheduleA.OrderedNodeIds);
    }

    [Fact]
    public void BuildSchedule_DeduplicatesNodeIds()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "a", "a", "b" }, CycleStart, SlotDuration);

        Assert.Equal(new[] { "a", "b" }, schedule.OrderedNodeIds);
    }

    [Fact]
    public void TransmittingNodeIdAt_ReturnsTheNodeWhoseSlotContainsThatInstant()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "a", "b", "c" }, CycleStart, SlotDuration);

        Assert.Equal("a", schedule.TransmittingNodeIdAt(CycleStart));
        Assert.Equal("a", schedule.TransmittingNodeIdAt(CycleStart + TimeSpan.FromSeconds(1.9)));
        Assert.Equal("b", schedule.TransmittingNodeIdAt(CycleStart + TimeSpan.FromSeconds(2)));
        Assert.Equal("c", schedule.TransmittingNodeIdAt(CycleStart + TimeSpan.FromSeconds(5.9)));
    }

    [Fact]
    public void TransmittingNodeIdAt_ReturnsNull_BeforeCycleStartOrAfterCycleEnd()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "a", "b" }, CycleStart, SlotDuration);

        Assert.Null(schedule.TransmittingNodeIdAt(CycleStart - TimeSpan.FromSeconds(1)));
        Assert.Null(schedule.TransmittingNodeIdAt(CycleStart + TimeSpan.FromSeconds(4))); // cycle is exactly 2 slots = 4s
    }

    [Fact]
    public void TransmittingNodeIdAt_ReturnsNull_WhenScheduleHasNoNodes()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(Array.Empty<string>(), CycleStart, SlotDuration);

        Assert.Null(schedule.TransmittingNodeIdAt(CycleStart));
    }

    [Fact]
    public void SlotStartFor_ReturnsEachNodesOwnSlotStart()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "a", "b", "c" }, CycleStart, SlotDuration);

        Assert.Equal(CycleStart, schedule.SlotStartFor("a"));
        Assert.Equal(CycleStart + TimeSpan.FromSeconds(2), schedule.SlotStartFor("b"));
        Assert.Equal(CycleStart + TimeSpan.FromSeconds(4), schedule.SlotStartFor("c"));
    }

    [Fact]
    public void SlotStartFor_ReturnsNull_ForANodeNotInTheSchedule()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "a", "b" }, CycleStart, SlotDuration);

        Assert.Null(schedule.SlotStartFor("late-joiner"));
    }

    [Fact]
    public void CycleDuration_IsSlotDurationTimesNodeCount()
    {
        var schedule = UltrasonicRoundRobinScheduler.BuildSchedule(new[] { "a", "b", "c" }, CycleStart, SlotDuration);

        Assert.Equal(TimeSpan.FromSeconds(6), schedule.CycleDuration);
    }
}
