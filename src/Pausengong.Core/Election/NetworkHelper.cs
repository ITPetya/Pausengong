using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Pausengong.Core.Election;

public static class NetworkHelper
{
    /// <summary>
    /// IPv4 address of the NIC that actually routes to the LAN. Prefers an interface with a
    /// default gateway (a real Ethernet/Wi-Fi adapter) over one without, since plain "first
    /// up, non-loopback NIC" previously could and did pick a virtual adapter (VPN, Hyper-V
    /// vEthernet, VMware/VirtualBox host-only, Docker, …) ahead of the real LAN adapter — those
    /// enumerate before physical NICs on some machines, and advertising that address to peers
    /// makes this node unreachable from other PCs on the actual LAN even though discovery and
    /// election otherwise look fine locally.
    /// </summary>
    public static string GetPrimaryIPv4Address()
    {
        var candidates = new List<string>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var properties = nic.GetIPProperties();
            var hasGateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (hasGateway)
                {
                    return unicast.Address.ToString();
                }

                candidates.Add(unicast.Address.ToString());
            }
        }

        return candidates.FirstOrDefault() ?? "127.0.0.1";
    }
}
