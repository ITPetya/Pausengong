using Pausengong.Core.Models;

namespace Pausengong.Core.Config;

public sealed record RoomConfig(IReadOnlyList<RoomGroup> Rooms, DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// Well-known id/name for the always-present landing room every newly seen node is
    /// auto-assigned to until a human drags it elsewhere. Replaces the old "Nicht zugeordnet"
    /// pseudo-list: that was never a real room, so <c>BreakScheduler</c> (which only ever draws
    /// against configured rooms) had nothing to ring against on a fresh install — a schedule
    /// with break times but zero rooms created would silently never ring anyone.
    /// </summary>
    public const string DefaultRoomId = "default";
    public const string DefaultRoomName = "Standardraum";

    public static RoomConfig Default { get; } = new(
        new[] { new RoomGroup(DefaultRoomId, DefaultRoomName, Array.Empty<string>()) },
        DateTimeOffset.MinValue);

    /// <summary>True for a config saved before the default landing room existed (e.g. an old
    /// install with zero rooms). <see cref="ConfigStore"/> self-heals this in memory on every load.</summary>
    public bool IsMissingDefaultRoom => !Rooms.Any(r => r.RoomId == DefaultRoomId);
}
