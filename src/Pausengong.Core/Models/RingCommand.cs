namespace Pausengong.Core.Models;

/// <summary>Wire payload for <c>pausengong/ring/target/{nodeId}</c> — tells the receiving node
/// which room it's ringing for. Always published non-retained: it's a one-shot command, not
/// status, and a freshly (re)connecting node must never replay an old one.</summary>
public sealed record RingCommand(string RoomName, DateTimeOffset AtTime);
