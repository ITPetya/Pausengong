namespace Pausengong.Core.Election;

/// <summary>
/// Pure priority-ordering rules for the succession list. Deliberately has no I/O so it can be
/// unit tested directly: earliest <see cref="SuccessionEntry.PriorityTimestamp"/> wins, ties
/// broken by ordinal NodeId comparison so every node computes the exact same order from the
/// same input, with no coordination required.
/// </summary>
public static class SuccessionOrdering
{
    public static int Compare(SuccessionEntry a, SuccessionEntry b)
    {
        var byTime = a.PriorityTimestamp.CompareTo(b.PriorityTimestamp);
        return byTime != 0 ? byTime : string.CompareOrdinal(a.NodeId, b.NodeId);
    }

    public static IReadOnlyList<SuccessionEntry> OrderByPriority(IEnumerable<SuccessionEntry> entries) =>
        entries.OrderBy(e => e, Comparer<SuccessionEntry>.Create(Compare)).ToList();

    public static SuccessionEntry? HighestPriority(IEnumerable<SuccessionEntry> entries)
    {
        SuccessionEntry? best = null;
        foreach (var entry in entries)
        {
            if (best is null || Compare(entry, best) < 0)
            {
                best = entry;
            }
        }

        return best;
    }

    /// <summary>True if <paramref name="candidate"/> should be Chef instead of <paramref name="incumbent"/>.</summary>
    public static bool HasHigherPriority(SuccessionEntry candidate, SuccessionEntry incumbent) =>
        Compare(candidate, incumbent) < 0;

    /// <summary>
    /// Given the ordered list and a node that just proved unavailable, returns the next
    /// candidate in line (used both for Chef failover and for room-draw fallback).
    /// </summary>
    public static SuccessionEntry? NextAfter(IReadOnlyList<SuccessionEntry> ordered, string nodeId)
    {
        var index = -1;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].NodeId == nodeId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return ordered.Count > 0 ? ordered[0] : null;
        }

        return index + 1 < ordered.Count ? ordered[index + 1] : null;
    }
}
