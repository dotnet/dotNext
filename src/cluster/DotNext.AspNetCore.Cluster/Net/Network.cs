using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;
using IServerAddresses = Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature;

namespace DotNext.Net
{
    using Hosting.Server.Features;

    internal static class Network
    {
        internal static bool IsIn(this IPAddress? address, IPNetwork network)
            => address is not null && network.Contains(address);

        internal static IPEndPoint? ToEndPoint(this Uri memberUri) => memberUri.HostNameType switch
        {
            UriHostNameType.IPv4 or UriHostNameType.IPv6 => new IPEndPoint(IPAddress.Parse(memberUri.Host), memberUri.Port),
            UriHostNameType.Dns when memberUri.IsLoopback => new IPEndPoint(IPAddress.Loopback, memberUri.Port),
            _ => null
        };

        internal static ICollection<IPEndPoint> GetHostingAddresses(this IServer server)
        {
            var feature = server.Features.Get<IServerAddresses>();
            if (feature is null || feature.Addresses.Count == 0)
                return Array.Empty<IPEndPoint>();
            var result = new HashSet<IPEndPoint>();
            var hint = server.Features.Get<HostAddressHintFeature>()?.Address;
            foreach (var address in feature.Addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    var endpoint = uri.ToEndPoint();
                    if (endpoint is null)
                    {
                        continue;
                    }
                    else if (endpoint.Address.IsOneOf(IPAddress.Any, IPAddress.IPv6Any))
                    {
                        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                        {
                            foreach (var nicAddr in nic.GetIPProperties().UnicastAddresses)
                                result.Add(new IPEndPoint(nicAddr.Address, endpoint.Port));
                        }
                    }
                    else
                    {
                        result.Add(endpoint);
                    }

                    // add host address hint if it is available
                    if (hint is not null)
                        result.Add(new IPEndPoint(hint, endpoint.Port));
                }
            }

            return result;
        }
    }
}
