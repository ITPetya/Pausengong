using Pausengong.Core.Models;
using Pausengong.Core.Neighbors;
using Xunit;

namespace Pausengong.Core.Tests;

public class NeighborGraphBuilderTests
{
    [Fact]
    public void BuildEdges_WithBothDirectionsReported_AveragesThemAndMarksBidirectional()
    {
        var reports = new[]
        {
            new NeighborReport("a", new Dictionary<string, double> { ["b"] = -60 }, DateTimeOffset.UtcNow),
            new NeighborReport("b", new Dictionary<string, double> { ["a"] = -70 }, DateTimeOffset.UtcNow),
        };

        var edges = NeighborGraphBuilder.BuildEdges(reports);

        var edge = Assert.Single(edges);
        Assert.Equal(-65, edge.SymmetrizedRssi);
        Assert.True(edge.IsBidirectional);
    }

    [Fact]
    public void BuildEdges_WithOnlyOneDirectionReported_KeepsItButMarksProvisional()
    {
        var reports = new[]
        {
            new NeighborReport("a", new Dictionary<string, double> { ["b"] = -60 }, DateTimeOffset.UtcNow),
        };

        var edges = NeighborGraphBuilder.BuildEdges(reports);

        var edge = Assert.Single(edges);
        Assert.Equal(-60, edge.SymmetrizedRssi);
        Assert.False(edge.IsBidirectional);
    }

    [Fact]
    public void BuildEdges_DoesNotProduceDuplicateEdges_ForTheSamePairInBothDirections()
    {
        var reports = new[]
        {
            new NeighborReport("a", new Dictionary<string, double> { ["b"] = -60 }, DateTimeOffset.UtcNow),
            new NeighborReport("b", new Dictionary<string, double> { ["a"] = -60 }, DateTimeOffset.UtcNow),
        };

        var edges = NeighborGraphBuilder.BuildEdges(reports);

        Assert.Single(edges);
    }

    [Fact]
    public void BuildEdges_WithMultiplePairs_ProducesOneEdgePerPair()
    {
        var reports = new[]
        {
            new NeighborReport("a", new Dictionary<string, double> { ["b"] = -50, ["c"] = -90 }, DateTimeOffset.UtcNow),
            new NeighborReport("b", new Dictionary<string, double> { ["a"] = -55 }, DateTimeOffset.UtcNow),
        };

        var edges = NeighborGraphBuilder.BuildEdges(reports);

        Assert.Equal(2, edges.Count);
        Assert.Contains(edges, e => new[] { e.NodeIdA, e.NodeIdB }.OrderBy(x => x).SequenceEqual(new[] { "a", "b" }.OrderBy(x => x)) && e.IsBidirectional);
        Assert.Contains(edges, e => new[] { e.NodeIdA, e.NodeIdB }.OrderBy(x => x).SequenceEqual(new[] { "a", "c" }.OrderBy(x => x)) && !e.IsBidirectional);
    }
}
