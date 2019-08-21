using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;
using IServerAddresses = Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature;

namespace DotNext.Net
{
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
                    if (memberUri.IsLoopback)
                        return new IPEndPoint(IPAddress.Loopback, memberUri.Port);
                    goto default;
                default:
                    return null;
            }
        }

        public static UriBuilder SetHostAndPort(this UriBuilder builder, IPEndPoint endpoint)
        {
            builder.Port = endpoint.Port;
            builder.Host = endpoint.Address.ToString();
            return builder;
        }

        internal static ICollection<IPEndPoint> GetHostingAddresses(this IServer server)
        {
            var feature = server.Features.Get<IServerAddresses>();
            if (feature is null || feature.Addresses.Count == 0)
                return Array.Empty<IPEndPoint>();
            var result = new HashSet<IPEndPoint>();
            foreach (var address in feature.Addresses)
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    var endpoint = uri.ToEndPoint();
                    if (endpoint is null)
                        continue;
                    else if (endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any))
                        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                            foreach (var nicAddr in nic.GetIPProperties().UnicastAddresses)
                                result.Add(new IPEndPoint(nicAddr.Address, endpoint.Port));
                    else
                        result.Add(endpoint);
                }
            return result;
        }
    }
}
