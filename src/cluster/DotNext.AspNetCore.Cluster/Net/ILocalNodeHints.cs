using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace DotNext.Net
{
    using HostAddressHintFeature = DotNext.Hosting.Server.Features.HostAddressHintFeature;

    internal interface ILocalNodeHints
    {
        /// <summary>
        /// Gets the address of the local node.
        /// </summary>
        IPAddress? HostAddressHint { get; }

        /// <summary>
        /// Gets DNS name of the local node visible to other nodes in the network.
        /// </summary>
        string? HostNameHint { get; }

        internal void SetupHostAddressHint(IFeatureCollection features)
        {
            var address = HostAddressHint;
            var name = HostNameHint;
            HostAddressHintFeature? feature = null;

            if (!features.IsReadOnly)
            {
                if (address is not null)
                    feature += address.ToEndPoint;

                if (!string.IsNullOrWhiteSpace(name))
                    feature += name.ToEndPoint;
            }

            if (feature is not null)
                features.Set(feature);
        }
    }
}