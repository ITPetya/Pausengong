using Pausengong.Core.Models;
using Xunit;

namespace Pausengong.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void RoomGroup_HoldsAssignedNodeIds()
    {
        var room = new RoomGroup("room-1", "Klassenzimmer 3B", new[] { "pc-a", "pc-b" });

        Assert.Equal("room-1", room.RoomId);
        Assert.Equal(2, room.NodeIds.Count);
    }
}
