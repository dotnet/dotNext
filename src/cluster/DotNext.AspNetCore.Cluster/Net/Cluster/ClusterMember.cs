using System.Collections.Generic;
using System.Net;

namespace DotNext.Net.Cluster
{
    internal static class ClusterMember
    {
        internal static bool Contains(this ICollection<IPEndPoint> endpoints, IClusterMember member)
            => endpoints.Contains(member.Endpoint);

        internal static bool Represents(this IPEndPoint endpoint, IClusterMember member)
            => member.Endpoint.Equals(endpoint);
    }
}
