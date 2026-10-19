using Pausengong.Core.Models;

namespace Pausengong.Core.Neighbors;

/// <summary>
/// Groups nodes into anonymous neighborhood clusters by connected components over a threshold
/// graph — an edge exists iff its symmetrized signal is at or above the given threshold. This is
/// deliberately a simpler stand-in for DBSCAN/Louvain: threshold-graph + connected-components is
/// essentially single-linkage clustering with eps = the threshold, which fits "walls/distance
/// cause a hard cutoff, group everything still connected" without an ML dependency. Pure and
/// stateless, shared by both room-detection tracks — see HANDOFF.md "Phase 7 replacement: BLE
/// neighborhood clustering" (step 5, Clustering) and "Track B: ultrasonic presence sensing".
/// Run it once per track with that track's own threshold — never feed it a mix of BLE and
/// ultrasonic edges, the units aren't comparable.
/// </summary>
public static class ClusterCalculator
{
    /// <summary>
    /// <paramref name="threshold"/> must be calibrated per deployment/hardware — it's
    /// deliberately a parameter here, not a hardcoded magic value. A node with no qualifying edge
    /// to anyone still comes back as its own singleton cluster rather than being dropped.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Cluster(
        IReadOnlyCollection<string> allNodeIds,
        IReadOnlyCollection<NeighborEdge> edges,
        double threshold)
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
            if (edge.SymmetrizedSignal < threshold)
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
