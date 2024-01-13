using System.Net;
using System.Net.NetworkInformation;

namespace DotNext.Net.NetworkInformation;

public sealed class MtuDiscoveryTests : Test
{
    private const int PingTimeout = 2000;

    // on Linux, .NET doesn't support custom payloads for unprivileged account
    [PlatformSpecificFact("windows")]
    public static void PingToCloudflare()
    {
        var address = IPAddress.Parse("1.1.1.1");
        using var discovery = new MtuDiscovery();
        if (discovery.Send(address, PingTimeout).Status == IPStatus.Success)
        {
            var result = discovery.Discover(address, PingTimeout, new MtuDiscoveryOptions());
            NotNull(result);
        }
    }

    [PlatformSpecificFact("windows")]
    public static async Task PingToOpenDNS()
    {
        var address = IPAddress.Parse("208.67.222.222");
        using var discovery = new MtuDiscovery();
        if (discovery.Send(address, PingTimeout).Status == IPStatus.Success)
        {
            var result = await discovery.DiscoverAsync(address, PingTimeout, new MtuDiscoveryOptions());
            NotNull(result);
        }
    }
}