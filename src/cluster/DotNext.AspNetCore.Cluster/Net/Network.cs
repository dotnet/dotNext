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

        private static async Task AddAddressAndAliasesAsync(this ICollection<EndPoint> endpoints, IPEndPoint ip)
        {
            endpoints.Add(ip);

            // converts IP address to know host names
            foreach (var alias in (await Dns.GetHostEntryAsync(ip.Address).ConfigureAwait(false)).Aliases)
                endpoints.Add(new DnsEndPoint(alias, ip.Port));
        }

        // TODO: Return type must be changed to IReadOnlySet<EndPoint> in .NET 6
        internal static async Task<ICollection<EndPoint>> GetHostingAddressesAsync(this IServer server)
        {
            var feature = server.Features.Get<IServerAddresses>();
            if (feature is null || feature.Addresses.Count == 0)
                return ImmutableHashSet<EndPoint>.Empty;
            var result = new HashSet<EndPoint>();
            var hint = server.Features.Get<HostAddressHintFeature>()?.Address;
            foreach (var address in feature.Addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    var endpoint = uri.ToEndPoint();
                    switch (endpoint)
                    {
                        case IPEndPoint ip:
                            if (ip.Address.IsOneOf(IPAddress.Any, IPAddress.IPv6Any))
                            {
                                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                                {
                                    foreach (var nicAddr in nic.GetIPProperties().UnicastAddresses)
                                        await result.AddAddressAndAliasesAsync(new IPEndPoint(nicAddr.Address, ip.Port)).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                await result.AddAddressAndAliasesAsync(ip).ConfigureAwait(false);
                            }

                            // add host address hint if it is available
                            if (hint is not null)
                                result.Add(new IPEndPoint(hint, ip.Port));
                            break;
                        case DnsEndPoint dns:
                            result.Add(dns);

                            // convert DNS name to IP addresses
                            foreach (var ip in await Dns.GetHostAddressesAsync(dns.Host).ConfigureAwait(false))
                                result.Add(new IPEndPoint(ip, dns.Port));
                            break;
                        default:
                            continue;
                    }
                }
            }

            result.TrimExcess();
            return result;
        }
    }
}
