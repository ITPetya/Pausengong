namespace Pausengong.Core.Models;

/// <summary>One room's currently-known ring pick, broadcast by the Chef so every node's UI can
/// show who's up next (or who last actually rang) without needing to run the draw itself.</summary>
public sealed record DrawPreviewEntry(string RoomId, string RoomName, string NodeId, string NodeLabel);
