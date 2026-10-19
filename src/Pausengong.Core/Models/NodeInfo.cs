namespace Pausengong.Core.Models;

public sealed record NodeInfo(string NodeId, string HostName, DateTimeOffset JoinedAt)
{
    public string? Address { get; init; }
    public string? RoomId { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public bool IsAvailable { get; init; } = true;

    /// <summary>Human-readable label for UI lists — hostname plus IP when known, otherwise just the raw id.</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(Address) ? HostName : $"{HostName} ({Address})";
}
