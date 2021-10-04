using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;

namespace DotNext.Net.NetworkInformation
{
    [ExcludeFromCodeCoverage]
    public sealed class MtuDiscoveryTests : Test
    {
        private const int PingTimeout = 2000;

        [Fact]
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

        [Fact]
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
}