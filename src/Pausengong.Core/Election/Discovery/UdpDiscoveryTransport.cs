using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace Pausengong.Core.Election.Discovery;

public sealed class UdpDiscoveryTransport : IUdpDiscoveryTransport
{
    public const int DiscoveryPort = 49531;

    private readonly UdpClient _udpClient;

    public UdpDiscoveryTransport(int? bindPort = null)
    {
        _udpClient = new UdpClient(bindPort ?? DiscoveryPort)
        {
            EnableBroadcast = true,
        };
    }

    public async Task SendAsync(DiscoveryEnvelope envelope, IPEndPoint destination, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
        await _udpClient.SendAsync(bytes, bytes.Length, destination);
    }

    public async Task<(DiscoveryEnvelope Envelope, IPEndPoint From)?> ReceiveAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            var result = await _udpClient.ReceiveAsync(linkedCts.Token);
            var envelope = JsonSerializer.Deserialize<DiscoveryEnvelope>(result.Buffer);
            return envelope is null ? null : (envelope, result.RemoteEndPoint);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // timed out, not caller-cancelled
        }
        catch (JsonException)
        {
            return null; // malformed/foreign datagram on our port — ignore
        }
    }

    public IReadOnlyList<IPAddress> GetSubnetBroadcastAddresses()
    {
        var result = new List<IPAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask is null)
                {
                    continue;
                }

                var ipBytes = unicast.Address.GetAddressBytes();
                var maskBytes = unicast.IPv4Mask.GetAddressBytes();
                var broadcastBytes = new byte[4];
                for (var i = 0; i < 4; i++)
                {
                    broadcastBytes[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);
                }

                result.Add(new IPAddress(broadcastBytes));
            }
        }

        return result;
    }

    public ValueTask DisposeAsync()
    {
        _udpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}
