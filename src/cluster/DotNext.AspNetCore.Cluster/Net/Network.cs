using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;
using IServerAddresses = Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature;

namespace DotNext.Net
{
    using Hosting.Server.Features;

    internal static class Network
    {
        internal static bool IsIn(this IPAddress? address, IPNetwork network)
            => address is not null && network.Contains(address);

        internal static EndPoint? ToEndPoint(this Uri memberUri) => memberUri.HostNameType switch
        {
            UriHostNameType.IPv4 or UriHostNameType.IPv6 => new IPEndPoint(IPAddress.Parse(memberUri.Host), memberUri.Port),
            UriHostNameType.Dns => memberUri.IsLoopback ? new IPEndPoint(IPAddress.Loopback, memberUri.Port) : new DnsEndPoint(memberUri.IdnHost, memberUri.Port),
            _ => null
        };

        private static void Resolve(this HostAddressHintFeature feature, ICollection<EndPoint> endPoints, int port)
        {
            foreach (HostAddressHintFeature f in feature.GetInvocationList())
                endPoints.Add(f(port));
        }

        internal static IPEndPoint ToEndPoint(this IPAddress address, int port)
            => new (address, port);

        internal static DnsEndPoint ToEndPoint(this string name, int port)
            => new (name, port);

        // TODO: Return type must be changed to IReadOnlySet<EndPoint> in .NET 6
        internal static async Task<ICollection<EndPoint>> GetHostingAddressesAsync(this IServer server)
        {
            var feature = server.Features.Get<IServerAddresses>();
            if (feature is null || feature.Addresses.Count == 0)
                return ImmutableHashSet<EndPoint>.Empty;
            var result = new HashSet<EndPoint>();
            var hint = server.Features.Get<HostAddressHintFeature>();
            foreach (var address in feature.Addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    var endpoint = uri.ToEndPoint();
                    int portHint;
                    switch (endpoint)
                    {
                        case IPEndPoint ip:
                            if (ip.Address.IsOneOf(IPAddress.Any, IPAddress.IPv6Any))
                            {
                                // advertise the current host as DNS endpoint reachable via 0.0.0.0
                                result.Add(new DnsEndPoint(Dns.GetHostName(), ip.Port));

                                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                                {
                                    foreach (var nicAddr in nic.GetIPProperties().UnicastAddresses)
                                        result.Add(new IPEndPoint(nicAddr.Address, ip.Port));
                                }
                            }
                            else
                            {
                                result.Add(ip);

                                // converts IP address to know host names
                                foreach (var alias in (await Dns.GetHostEntryAsync(ip.Address).ConfigureAwait(false)).Aliases)
                                    result.Add(new DnsEndPoint(alias, ip.Port));
                            }

                            portHint = ip.Port;
                            break;
                        case DnsEndPoint dns:
                            result.Add(dns);

                            // convert DNS name to IP addresses
                            foreach (var ip in await Dns.GetHostAddressesAsync(dns.Host).ConfigureAwait(false))
                                result.Add(new IPEndPoint(ip, dns.Port));

                            portHint = dns.Port;
                            break;
                        default:
                            continue;
                    }

                    // add host address hint if it is available
                    hint?.Resolve(result, portHint);
                }
            }

            result.TrimExcess();
            return result;
        }
    }
}
