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
        private static readonly IPAddress Any = new IPAddress(new byte[] { 0, 0, 0, 0 });

        internal static bool IsIn(this IPAddress address, IPNetwork network) => network.Contains(address);

        internal static IPEndPoint ToEndPoint(this Uri memberUri)
        {
            switch (memberUri.HostNameType)
            {
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    return new IPEndPoint(IPAddress.Parse(memberUri.Host), memberUri.Port);
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
            foreach (var address in feature.Addresses)
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    var endpoint = uri.ToEndPoint();
                    if (endpoint is null)
                        continue;
                    else if (endpoint.Address.Equals(Any))
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
