using System;
using System.Net;

namespace DotNext.Hosting.Server.Features
{
    internal sealed class HostAddressHintFeature
    {
        internal readonly IPAddress Address;

        internal HostAddressHintFeature(IPAddress address) => Address = address ?? throw new ArgumentNullException(nameof(address));
    }
}
