namespace Pausengong.Core.Config;

/// <summary>
/// Stable per-machine identity persisted locally so the MQTT {pc-id} topic segment
/// survives hostname changes / DHCP renewals.
/// </summary>
public sealed record NodeIdentity(string NodeId, string HostName, DateTimeOffset CreatedAt)
{
    public static NodeIdentity CreateNew() =>
        new(Guid.NewGuid().ToString("N"), Environment.MachineName, DateTimeOffset.UtcNow);
}
