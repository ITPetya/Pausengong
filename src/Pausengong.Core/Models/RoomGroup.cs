namespace Pausengong.Core.Models;

public sealed record RoomGroup(string RoomId, string Name, IReadOnlyList<string> NodeIds);
