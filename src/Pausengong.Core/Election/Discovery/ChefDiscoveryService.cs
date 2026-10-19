using System.Net;

namespace Pausengong.Core.Election.Discovery;

/// <summary>
/// Implements the LAN discovery protocol used before any MQTT connection exists: find the
/// current Chef's address (cached unicast first, subnet broadcast fallback), and separately,
/// answer other nodes' discovery requests while acting as Chef.
/// </summary>
public sealed class ChefDiscoveryService
{
    private static readonly TimeSpan UnicastAttemptTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan[] UnicastBackoff = { TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(600) };
    private const int BroadcastAttempts = 3;
    private static readonly TimeSpan BroadcastAttemptSpacing = TimeSpan.FromMilliseconds(800);

    private readonly IUdpDiscoveryTransport _transport;

    public ChefDiscoveryService(IUdpDiscoveryTransport transport)
    {
        _transport = transport;
    }

    /// <summary>Raised when, while acting as Chef, a <c>chef_announce</c> from a different chief is heard.</summary>
    public event Action<SuccessionEntry>? ConflictingChefAnnounced;

    /// <summary>
    /// Finds the current Chef. Tries a cached last-known address by unicast first (cheap,
    /// quiet); falls back to subnet broadcast if there's no cache or it doesn't answer.
    /// Null means nobody answered — caller should self-promote.
    /// </summary>
    public async Task<SuccessionEntry?> DiscoverChefAsync(string selfNodeId, SuccessionEntry? cachedChef, CancellationToken ct = default)
    {
        if (cachedChef is not null)
        {
            var destination = new IPEndPoint(IPAddress.Parse(cachedChef.Address), UdpDiscoveryTransport.DiscoveryPort);
            var unicastResult = await TryDiscoverAsync(selfNodeId, new[] { destination }, UnicastBackoff.Length + 1, UnicastAttemptTimeout, UnicastBackoff, ct);
            if (unicastResult is not null)
            {
                return unicastResult;
            }
        }

        var broadcastDestinations = _transport.GetSubnetBroadcastAddresses()
            .Select(addr => new IPEndPoint(addr, UdpDiscoveryTransport.DiscoveryPort))
            .ToArray();
        if (broadcastDestinations.Length == 0)
        {
            return null;
        }

        var spacing = Enumerable.Repeat(BroadcastAttemptSpacing, BroadcastAttempts - 1).ToArray();
        return await TryDiscoverAsync(selfNodeId, broadcastDestinations, BroadcastAttempts, BroadcastAttemptSpacing, spacing, ct);
    }

    private async Task<SuccessionEntry?> TryDiscoverAsync(
        string selfNodeId,
        IReadOnlyList<IPEndPoint> destinations,
        int attempts,
        TimeSpan perAttemptTimeout,
        IReadOnlyList<TimeSpan> delaysBetweenAttempts,
        CancellationToken ct)
    {
        var request = DiscoveryEnvelope.Discover(selfNodeId);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            foreach (var destination in destinations)
            {
                await _transport.SendAsync(request, destination, ct);
            }

            var received = await _transport.ReceiveAsync(perAttemptTimeout, ct);
            if (received is { Envelope.Type: DiscoveryEnvelope.TypeAnnounce } result)
            {
                return ToSuccessionEntry(result.Envelope);
            }

            if (attempt < delaysBetweenAttempts.Count)
            {
                await Task.Delay(delaysBetweenAttempts[attempt], ct);
            }
        }

        return null;
    }

    /// <summary>
    /// Runs until cancelled: answers <c>chef_discover</c> requests when <paramref name="isSelfChief"/>
    /// is true, and raises <see cref="ConflictingChefAnnounced"/> on a <c>chef_announce</c> from
    /// someone else while we believe we're Chef.
    /// </summary>
    public async Task RunResponderLoopAsync(Func<bool> isSelfChief, Func<SuccessionEntry> getSelfEntry, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var received = await _transport.ReceiveAsync(TimeSpan.FromSeconds(2), ct);
            if (received is null)
            {
                continue;
            }

            var (envelope, from) = received.Value;
            switch (envelope.Type)
            {
                case DiscoveryEnvelope.TypeDiscover when isSelfChief():
                    await _transport.SendAsync(DiscoveryEnvelope.Announce(getSelfEntry()), from, ct);
                    break;

                case DiscoveryEnvelope.TypeAnnounce when isSelfChief() && envelope.ChefId != getSelfEntry().NodeId:
                    ConflictingChefAnnounced?.Invoke(ToSuccessionEntry(envelope));
                    break;
            }
        }
    }

    private static SuccessionEntry ToSuccessionEntry(DiscoveryEnvelope envelope) => new(
        envelope.ChefId!,
        HostName: envelope.ChefHostName ?? envelope.ChefId!,
        Address: envelope.Address!,
        MqttPort: envelope.MqttPort!.Value,
        PriorityTimestamp: envelope.PriorityTimestamp!.Value);
}
