using Pausengong.Core.Models;

namespace Pausengong.Core.Scheduling;

/// <summary>Randomly picks which node in a room rings, with a fallback order in case the first
/// pick doesn't confirm. Pure and stateless — availability is passed in, not looked up.</summary>
public static class DrawService
{
    /// <summary>Returns null if none of the room's members are currently available.</summary>
    public static DrawResult? Draw(RoomGroup room, IReadOnlySet<string> availableNodeIds, Random random)
    {
        var candidates = room.NodeIds.Where(availableNodeIds.Contains).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        Shuffle(candidates, random);
        return new DrawResult(room.RoomId, candidates[0], candidates);
    }

    private static void Shuffle<T>(IList<T> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
