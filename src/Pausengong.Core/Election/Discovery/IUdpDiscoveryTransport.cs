using System.Net;

namespace Pausengong.Core.Election.Discovery;

/// <summary>
/// Seam over the raw UDP socket so <see cref="ChefDiscoveryService"/>'s protocol logic can be
/// unit tested against an in-memory fake instead of real sockets.
/// </summary>
public interface IUdpDiscoveryTransport : IAsyncDisposable
{
    Task SendAsync(DiscoveryEnvelope envelope, IPEndPoint destination, CancellationToken ct);

    /// <summary>Waits up to <paramref name="timeout"/> for the next datagram; null on timeout.</summary>
    Task<(DiscoveryEnvelope Envelope, IPEndPoint From)?> ReceiveAsync(TimeSpan timeout, CancellationToken ct);

    IReadOnlyList<IPAddress> GetSubnetBroadcastAddresses();
}
