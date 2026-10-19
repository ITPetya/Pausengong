using Pausengong.Core.Models;

namespace Pausengong.Core.Neighbors;

/// <summary>
/// Groups nodes into anonymous neighborhood clusters by connected components over a threshold
/// graph — an edge exists iff its symmetrized RSSI is at or above the given threshold. This is
/// deliberately a simpler stand-in for DBSCAN/Louvain: threshold-graph + connected-components is
/// essentially single-linkage clustering with eps = the RSSI threshold, which fits "walls cause a
/// hard cutoff, group everything still connected" without an ML dependency. Pure and stateless —
/// see HANDOFF.md "Phase 7 replacement: BLE neighborhood clustering", step 5 (Clustering).
/// </summary>
public static class ClusterCalculator
{
    /// <summary>
    /// <paramref name="thresholdRssiDbm"/> must be calibrated per deployment/hardware — it's
    /// deliberately a parameter here, not a hardcoded magic value. A node with no qualifying edge
    /// to anyone still comes back as its own singleton cluster rather than being dropped.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Cluster(
        IReadOnlyCollection<string> allNodeIds,
        IReadOnlyCollection<NeighborEdge> edges,
        double thresholdRssiDbm)
    {
        var parent = allNodeIds.ToDictionary(id => id, id => id);

        string Find(string id)
        {
            while (parent[id] != id)
            {
                parent[id] = parent[parent[id]]; // path halving
                id = parent[id];
            }

            return id;
        }

        void Union(string a, string b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
            {
                parent[rootA] = rootB;
            }
        }

        foreach (var edge in edges)
        {
            if (edge.SymmetrizedRssi < thresholdRssiDbm)
            {
                continue;
            }

            if (!parent.ContainsKey(edge.NodeIdA) || !parent.ContainsKey(edge.NodeIdB))
            {
                continue; // edge references a node outside the given node set — ignore
            }

            Union(edge.NodeIdA, edge.NodeIdB);
        }

        return allNodeIds
            .GroupBy(Find)
            .Select(g => (IReadOnlyList<string>)g.ToList())
            .ToList();
    }
}
