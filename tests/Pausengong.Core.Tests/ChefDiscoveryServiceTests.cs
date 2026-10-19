using System.Net;
using Pausengong.Core.Election;
using Pausengong.Core.Election.Discovery;
using Xunit;

namespace Pausengong.Core.Tests;

public class ChefDiscoveryServiceTests
{
    private static SuccessionEntry Chef(string id) =>
        new(id, HostName: id, Address: "192.168.1.42", MqttPort: 18830, PriorityTimestamp: DateTimeOffset.UtcNow);

    [Fact]
    public async Task DiscoverChefAsync_WithCachedChef_UnicastsToItAndReturnsReply()
    {
        var transport = new FakeUdpDiscoveryTransport();
        var cached = Chef("pc-chef");
        var replyFrom = new IPEndPoint(IPAddress.Parse(cached.Address), UdpDiscoveryTransport.DiscoveryPort);
        transport.EnqueueIncoming(DiscoveryEnvelope.Announce(cached), replyFrom);
        var service = new ChefDiscoveryService(transport);

        var result = await service.DiscoverChefAsync("pc-self", cached);

        Assert.NotNull(result);
        Assert.Equal(cached.NodeId, result!.NodeId);
        Assert.Equal(cached.Address, result.Address);
        Assert.Single(transport.Sent);
        Assert.Equal(cached.Address, transport.Sent[0].Destination.Address.ToString());
        Assert.Equal(UdpDiscoveryTransport.DiscoveryPort, transport.Sent[0].Destination.Port);
    }

    [Fact]
    public async Task DiscoverChefAsync_NoCache_BroadcastsAndReturnsFirstReply()
    {
        var transport = new FakeUdpDiscoveryTransport
        {
            BroadcastAddresses = new[] { IPAddress.Parse("192.168.1.255") },
        };
        var chef = Chef("pc-chef");
        transport.EnqueueIncoming(DiscoveryEnvelope.Announce(chef), new IPEndPoint(IPAddress.Parse(chef.Address), UdpDiscoveryTransport.DiscoveryPort));
        var service = new ChefDiscoveryService(transport);

        var result = await service.DiscoverChefAsync("pc-self", cachedChef: null);

        Assert.NotNull(result);
        Assert.Equal("pc-chef", result!.NodeId);
        Assert.Contains(transport.Sent, s => s.Destination.Address.ToString() == "192.168.1.255");
    }

    [Fact]
    public async Task DiscoverChefAsync_NobodyReplies_ReturnsNull()
    {
        var transport = new FakeUdpDiscoveryTransport
        {
            BroadcastAddresses = new[] { IPAddress.Parse("192.168.1.255") },
        };
        var service = new ChefDiscoveryService(transport);

        var result = await service.DiscoverChefAsync("pc-self", cachedChef: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task RunResponderLoopAsync_AnswersDiscoverRequest_WhenSelfIsChief()
    {
        var transport = new FakeUdpDiscoveryTransport();
        var self = Chef("pc-self");
        var requester = new IPEndPoint(IPAddress.Parse("192.168.1.10"), UdpDiscoveryTransport.DiscoveryPort);
        transport.EnqueueIncoming(DiscoveryEnvelope.Discover("pc-other"), requester);
        var service = new ChefDiscoveryService(transport);
        using var cts = new CancellationTokenSource();

        var loopTask = service.RunResponderLoopAsync(() => true, () => self, cts.Token);
        await WaitUntilAsync(() => transport.Sent.Count > 0);
        cts.Cancel();
        await AwaitLoopAsync(loopTask);

        var sent = Assert.Single(transport.Sent);
        Assert.Equal(DiscoveryEnvelope.TypeAnnounce, sent.Envelope.Type);
        Assert.Equal(self.NodeId, sent.Envelope.ChefId);
        Assert.Equal(requester, sent.Destination);
    }

    [Fact]
    public async Task RunResponderLoopAsync_RaisesConflict_WhenForeignAnnounceReceivedWhileSelfChief()
    {
        var transport = new FakeUdpDiscoveryTransport();
        var self = Chef("pc-self");
        var otherChef = Chef("pc-other-chief");
        transport.EnqueueIncoming(DiscoveryEnvelope.Announce(otherChef), new IPEndPoint(IPAddress.Parse(otherChef.Address), UdpDiscoveryTransport.DiscoveryPort));
        var service = new ChefDiscoveryService(transport);
        SuccessionEntry? conflicted = null;
        service.ConflictingChefAnnounced += entry => conflicted = entry;
        using var cts = new CancellationTokenSource();

        var loopTask = service.RunResponderLoopAsync(() => true, () => self, cts.Token);
        await WaitUntilAsync(() => conflicted is not null);
        cts.Cancel();
        await AwaitLoopAsync(loopTask);

        Assert.NotNull(conflicted);
        Assert.Equal("pc-other-chief", conflicted!.NodeId);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private static async Task AwaitLoopAsync(Task loopTask)
    {
        try
        {
            await loopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
