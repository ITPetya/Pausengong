namespace Pausengong.Core.Election.Discovery;

/// <summary>
/// Single JSON message shape used for both discovery request and reply datagrams, distinguished
/// by <see cref="Type"/>. Kept as one type (rather than a polymorphic hierarchy) since the two
/// shapes are small and this avoids a custom JSON converter for a two-case protocol.
/// </summary>
public sealed record DiscoveryEnvelope(
    string Type,
    int ProtocolVersion,
    string? FromId,
    string? ChefId,
    string? ChefHostName,
    string? Address,
    int? MqttPort,
    DateTimeOffset? PriorityTimestamp)
{
    public const string TypeDiscover = "chef_discover";
    public const string TypeAnnounce = "chef_announce";
    public const int CurrentProtocolVersion = 1;

    public static DiscoveryEnvelope Discover(string fromId) =>
        new(TypeDiscover, CurrentProtocolVersion, fromId, null, null, null, null, null);

    public static DiscoveryEnvelope Announce(SuccessionEntry chef) =>
        new(TypeAnnounce, CurrentProtocolVersion, null, chef.NodeId, chef.HostName, chef.Address, chef.MqttPort, chef.PriorityTimestamp);
}
