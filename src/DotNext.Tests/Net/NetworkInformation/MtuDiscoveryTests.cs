using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;

namespace DotNext.Net.NetworkInformation
{
    [ExcludeFromCodeCoverage]
    public sealed class MtuDiscoveryTests : Test
    {
        [EnvarDependentFact("InternetAccess", "true", "true")]
        public static void PingToCloudflare()
        {
            using var discovery = new MtuDiscovery();
            var result = discovery.Discover(IPAddress.Parse("1.1.1.1"), 2000, new MtuDiscoveryOptions());
            NotNull(result);
        }

        [EnvarDependentFact("InternetAccess", "true", "true")]
        public static async Task PingToOpenDNS()
        {
            using var discovery = new MtuDiscovery();
            var result = await discovery.DiscoverAsync(IPAddress.Parse("208.67.222.222"), 2000, new MtuDiscoveryOptions());
            NotNull(result);
        }
    }
}