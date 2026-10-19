using System.Net;
using Pausengong.Core.Election.Discovery;

namespace Pausengong.Core.Tests;

/// <summary>In-memory stand-in for the real UDP socket so discovery protocol logic can be tested without a network.</summary>
internal sealed class FakeUdpDiscoveryTransport : IUdpDiscoveryTransport
{
    private readonly Queue<(DiscoveryEnvelope Envelope, IPEndPoint From)?> _incoming = new();

    public List<(DiscoveryEnvelope Envelope, IPEndPoint Destination)> Sent { get; } = new();

    public IReadOnlyList<IPAddress> BroadcastAddresses { get; set; } = new[] { IPAddress.Parse("192.168.1.255") };

    public void EnqueueIncoming(DiscoveryEnvelope envelope, IPEndPoint from) => _incoming.Enqueue((envelope, from));

    public void EnqueueTimeout() => _incoming.Enqueue(null);

    public Task SendAsync(DiscoveryEnvelope envelope, IPEndPoint destination, CancellationToken ct)
    {
        Sent.Add((envelope, destination));
        return Task.CompletedTask;
    }

    public async Task<(DiscoveryEnvelope Envelope, IPEndPoint From)?> ReceiveAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (_incoming.Count > 0)
        {
            return _incoming.Dequeue();
        }

        // Avoid busy-spinning the caller's loop while "waiting" for a datagram that never arrives.
        await Task.Delay(5, CancellationToken.None);
        return null;
    }

    public IReadOnlyList<IPAddress> GetSubnetBroadcastAddresses() => BroadcastAddresses;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
