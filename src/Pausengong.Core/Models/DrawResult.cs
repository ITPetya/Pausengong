namespace Pausengong.Core.Models;

public sealed record DrawResult(string RoomId, string DrawnNodeId, IReadOnlyList<string> CandidateOrder);
