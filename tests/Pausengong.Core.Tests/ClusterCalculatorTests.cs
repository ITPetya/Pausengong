using Pausengong.Core.Models;
using Pausengong.Core.Neighbors;
using Xunit;

namespace Pausengong.Core.Tests;

public class ClusterCalculatorTests
{
    [Fact]
    public void Cluster_GroupsNodesConnectedByEdgesAboveThreshold_IntoOneCluster()
    {
        var nodes = new[] { "a", "b", "c" };
        var edges = new[]
        {
            new NeighborEdge("a", "b", -60, IsBidirectional: true),
            new NeighborEdge("b", "c", -55, IsBidirectional: true),
        };

        var clusters = ClusterCalculator.Cluster(nodes, edges, thresholdRssiDbm: -70);

        var cluster = Assert.Single(clusters);
        Assert.Equal(nodes.OrderBy(x => x), cluster.OrderBy(x => x));
    }

    [Fact]
    public void Cluster_KeepsNodesSeparate_WhenEdgeIsBelowThreshold()
    {
        var nodes = new[] { "a", "b" };
        var edges = new[] { new NeighborEdge("a", "b", -85, IsBidirectional: true) };

        var clusters = ClusterCalculator.Cluster(nodes, edges, thresholdRssiDbm: -70);

        Assert.Equal(2, clusters.Count);
        Assert.All(clusters, c => Assert.Single(c));
    }

    [Fact]
    public void Cluster_IncludesEdgeExactlyAtThreshold()
    {
        var nodes = new[] { "a", "b" };
        var edges = new[] { new NeighborEdge("a", "b", -70, IsBidirectional: true) };

        var clusters = ClusterCalculator.Cluster(nodes, edges, thresholdRssiDbm: -70);

        Assert.Single(clusters);
    }

    [Fact]
    public void Cluster_TransitivelyConnectsThroughAMiddleNode_EvenWithoutADirectEdge()
    {
        // a-c has no edge at all, but a-b and b-c are both strong -> all three still one cluster.
        var nodes = new[] { "a", "b", "c" };
        var edges = new[]
        {
            new NeighborEdge("a", "b", -50, IsBidirectional: true),
            new NeighborEdge("b", "c", -50, IsBidirectional: true),
        };

        var clusters = ClusterCalculator.Cluster(nodes, edges, thresholdRssiDbm: -70);

        Assert.Single(clusters);
    }

    [Fact]
    public void Cluster_ProducesTwoSeparateGroups_WhenTwoPairsAreIsolatedFromEachOther()
    {
        var nodes = new[] { "a", "b", "c", "d" };
        var edges = new[]
        {
            new NeighborEdge("a", "b", -50, IsBidirectional: true),
            new NeighborEdge("c", "d", -50, IsBidirectional: true),
        };

        var clusters = ClusterCalculator.Cluster(nodes, edges, thresholdRssiDbm: -70);

        Assert.Equal(2, clusters.Count);
        Assert.Contains(clusters, c => c.OrderBy(x => x).SequenceEqual(new[] { "a", "b" }));
        Assert.Contains(clusters, c => c.OrderBy(x => x).SequenceEqual(new[] { "c", "d" }));
    }

    [Fact]
    public void Cluster_WithNoEdgesAtAll_ReturnsEveryNodeAsItsOwnSingletonCluster()
    {
        var nodes = new[] { "a", "b", "c" };

        var clusters = ClusterCalculator.Cluster(nodes, Array.Empty<NeighborEdge>(), thresholdRssiDbm: -70);

        Assert.Equal(3, clusters.Count);
        Assert.All(clusters, c => Assert.Single(c));
    }
}
