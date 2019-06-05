using System.Net;

namespace DotNext.Net
{
    internal static class IPAddressExtensions
    {
        internal static bool IsIn(this IPAddress address, IPNetwork network) => network.Contains(address);
    }
}
