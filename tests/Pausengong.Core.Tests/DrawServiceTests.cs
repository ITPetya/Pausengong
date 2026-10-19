using Pausengong.Core.Models;
using Pausengong.Core.Scheduling;
using Xunit;

namespace Pausengong.Core.Tests;

public class DrawServiceTests
{
    [Fact]
    public void Draw_ReturnsNull_WhenNoRoomMemberIsAvailable()
    {
        var room = new RoomGroup("room-1", "Büro", new[] { "a", "b" });
        var available = new HashSet<string> { "c" }; // none of the room's members

        var result = DrawService.Draw(room, available, new Random(1));

        Assert.Null(result);
    }

    [Fact]
    public void Draw_OnlyConsidersAvailableRoomMembers()
    {
        var room = new RoomGroup("room-1", "Büro", new[] { "a", "b", "c" });
        var available = new HashSet<string> { "b" }; // "a"/"c" are room members but offline

        var result = DrawService.Draw(room, available, new Random(1));

        Assert.NotNull(result);
        Assert.Equal("b", result!.DrawnNodeId);
        Assert.Equal(new[] { "b" }, result.CandidateOrder);
    }

    [Fact]
    public void Draw_CandidateOrderContainsExactlyTheAvailableMembers_DrawnNodeIsFirstInOrder()
    {
        var room = new RoomGroup("room-1", "Büro", new[] { "a", "b", "c", "d" });
        var available = new HashSet<string> { "a", "b", "c" };

        var result = DrawService.Draw(room, available, new Random(42));

        Assert.NotNull(result);
        Assert.Equal(new[] { "a", "b", "c" }, result!.CandidateOrder.OrderBy(x => x));
        Assert.Equal(result.CandidateOrder[0], result.DrawnNodeId);
    }

    [Fact]
    public void Draw_IsDeterministic_ForAGivenSeed()
    {
        var room = new RoomGroup("room-1", "Büro", new[] { "a", "b", "c", "d", "e" });
        var available = new HashSet<string> { "a", "b", "c", "d", "e" };

        var first = DrawService.Draw(room, available, new Random(7));
        var second = DrawService.Draw(room, available, new Random(7));

        Assert.Equal(first!.CandidateOrder, second!.CandidateOrder);
    }
}
