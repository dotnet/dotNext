using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;
using IServerAddresses = Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature;

namespace DotNext.Net
{
    using AspNetCore.Hosting.Server.Features;

    internal static class Network
    {
        internal static bool IsIn(this IPAddress address, IPNetwork network) => network.Contains(address);

        internal static IPEndPoint ToEndPoint(this Uri memberUri)
        {
            switch (memberUri.HostNameType)
            {
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    return new IPEndPoint(IPAddress.Parse(memberUri.Host), memberUri.Port);
                case UriHostNameType.Dns:
                    return memberUri.IsLoopback ? new IPEndPoint(IPAddress.Loopback, memberUri.Port) : null;
                default:
                    return null;
            }
        }

        internal static ICollection<IPEndPoint> GetHostingAddresses(this IServer server)
        {
            var feature = server.Features.Get<IServerAddresses>();
            if (feature is null || feature.Addresses.Count == 0)
                return Array.Empty<IPEndPoint>();
            var result = new HashSet<IPEndPoint>();
            var hint = server.Features.Get<HostAddressHintFeature>()?.Address;
            foreach (var address in feature.Addresses)
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    var endpoint = uri.ToEndPoint();
                    if (endpoint is null)
                        continue;
                    else if (endpoint.Address.IsOneOf(IPAddress.Any, IPAddress.IPv6Any))
                        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                            foreach (var nicAddr in nic.GetIPProperties().UnicastAddresses)
                                result.Add(new IPEndPoint(nicAddr.Address, endpoint.Port));
                    else
                        result.Add(endpoint);
                    //add host address hint if it is available
                    if (!(hint is null))
                        result.Add(new IPEndPoint(hint, endpoint.Port));
                }
            return result;
        }
    }
}
