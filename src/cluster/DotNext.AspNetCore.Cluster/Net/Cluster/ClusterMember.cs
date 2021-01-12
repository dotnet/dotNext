using System.Collections.Generic;
using System.Net;

namespace DotNext.Net.Cluster
{
    internal static class ClusterMember
    {
        /// <summary>
        /// Determines whether the collection of end points contains the end point of the cluster member.
        /// </summary>
        /// <param name="endpoints">The collection of end points.</param>
        /// <param name="member">The cluster member.</param>
        /// <returns><see langword="true"/> if <paramref name="endpoints"/> contains <see cref="IClusterMember.EndPoint"/> if <paramref name="member"/>.</returns>
        internal static bool Contains(this ICollection<IPEndPoint> endpoints, IClusterMember member)
            => member.EndPoint is IPEndPoint ip && endpoints.Contains(ip);
    }
}