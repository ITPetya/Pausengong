using Pausengong.Core.Models;

namespace Pausengong.Core.Neighbors;

/// <summary>
/// Turns raw per-node BLE observations into a symmetric adjacency structure. Pure and stateless
/// — see HANDOFF.md "Phase 7 replacement: BLE neighborhood clustering", step 4 (Aggregation).
/// BLE RSSI isn't perfectly symmetric A→B vs. B→A (radio/antenna differences), so where both
/// directions are known they're averaged; a one-directional-only observation is kept but flagged
/// as weaker/provisional evidence via <see cref="NeighborEdge.IsBidirectional"/>.
/// </summary>
public static class NeighborGraphBuilder
{
    /// <summary>One report per observer node is expected (the latest one); if more than one is
    /// passed for the same observer, later entries in <paramref name="reports"/> win.</summary>
    public static IReadOnlyList<NeighborEdge> BuildEdges(IReadOnlyCollection<NeighborReport> reports)
    {
        var directed = new Dictionary<(string From, string To), double>();
        foreach (var report in reports)
        {
            foreach (var (observedNodeId, rssi) in report.ObservedRssiByNodeId)
            {
                directed[(report.ObserverNodeId, observedNodeId)] = rssi;
            }
        }

        var pairs = new HashSet<(string A, string B)>();
        foreach (var (from, to) in directed.Keys)
        {
            pairs.Add(Canonicalize(from, to));
        }

        var edges = new List<NeighborEdge>();
        foreach (var (a, b) in pairs)
        {
            var hasAToB = directed.TryGetValue((a, b), out var aToB);
            var hasBToA = directed.TryGetValue((b, a), out var bToA);

            double symmetrized;
            bool bidirectional;
            if (hasAToB && hasBToA)
            {
                symmetrized = (aToB + bToA) / 2.0;
                bidirectional = true;
            }
            else
            {
                symmetrized = hasAToB ? aToB : bToA;
                bidirectional = false;
            }

            edges.Add(new NeighborEdge(a, b, symmetrized, bidirectional));
        }

        return edges;
    }

    private static (string A, string B) Canonicalize(string x, string y) =>
        string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
}
