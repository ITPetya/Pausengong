using Pausengong.Core.Election;
using Xunit;

namespace Pausengong.Core.Tests;

public class SuccessionOrderingTests
{
    private static SuccessionEntry Entry(string id, DateTimeOffset ts) =>
        new(id, HostName: id, Address: "10.0.0.1", MqttPort: 18830, PriorityTimestamp: ts);

    [Fact]
    public void OrderByPriority_SortsByEarliestTimestampFirst()
    {
        var t0 = DateTimeOffset.UtcNow;
        var a = Entry("pc-a", t0.AddMinutes(2));
        var b = Entry("pc-b", t0);
        var c = Entry("pc-c", t0.AddMinutes(1));

        var ordered = SuccessionOrdering.OrderByPriority(new[] { a, b, c });

        Assert.Equal(new[] { "pc-b", "pc-c", "pc-a" }, ordered.Select(e => e.NodeId));
    }

    [Fact]
    public void OrderByPriority_TiesBrokenByOrdinalNodeId()
    {
        var t0 = DateTimeOffset.UtcNow;
        var a = Entry("pc-z", t0);
        var b = Entry("pc-a", t0);

        var ordered = SuccessionOrdering.OrderByPriority(new[] { a, b });

        Assert.Equal(new[] { "pc-a", "pc-z" }, ordered.Select(e => e.NodeId));
    }

    [Fact]
    public void HasHigherPriority_EarlierTimestampWins()
    {
        var t0 = DateTimeOffset.UtcNow;
        var earlier = Entry("pc-a", t0);
        var later = Entry("pc-b", t0.AddSeconds(1));

        Assert.True(SuccessionOrdering.HasHigherPriority(earlier, later));
        Assert.False(SuccessionOrdering.HasHigherPriority(later, earlier));
    }

    [Fact]
    public void NextAfter_ReturnsFollowingEntryInOrder()
    {
        var t0 = DateTimeOffset.UtcNow;
        var ordered = SuccessionOrdering.OrderByPriority(new[]
        {
            Entry("pc-a", t0),
            Entry("pc-b", t0.AddMinutes(1)),
            Entry("pc-c", t0.AddMinutes(2)),
        });

        Assert.Equal("pc-b", SuccessionOrdering.NextAfter(ordered, "pc-a")!.NodeId);
        Assert.Null(SuccessionOrdering.NextAfter(ordered, "pc-c"));
    }

    [Fact]
    public void NextAfter_UnknownNodeId_ReturnsHighestPriorityEntry()
    {
        var t0 = DateTimeOffset.UtcNow;
        var ordered = SuccessionOrdering.OrderByPriority(new[] { Entry("pc-a", t0), Entry("pc-b", t0.AddMinutes(1)) });

        Assert.Equal("pc-a", SuccessionOrdering.NextAfter(ordered, "unknown-pc")!.NodeId);
    }
}
