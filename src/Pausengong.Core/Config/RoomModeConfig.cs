using Pausengong.Core.Models;

namespace Pausengong.Core.Config;

public sealed record RoomModeConfig(RoomMode Mode, DateTimeOffset UpdatedAt)
{
    public static RoomModeConfig Default { get; } = new(RoomMode.Manual, DateTimeOffset.MinValue);
}
