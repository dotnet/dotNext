using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Provides various extension methods that can be used to match cluster member
    /// by its network address.
    /// </summary>
    public static class ClusterMemberEndpoint
    {
        /// <summary>
        /// Determines whether the collection of end points contains the end point of the cluster member.
        /// </summary>
        /// <param name="endpoints">The collection of end points.</param>
        /// <param name="member">The cluster member.</param>
        /// <returns><see langword="true"/> if <paramref name="endpoints"/> contains <see cref="IClusterMember.Endpoint"/> if <paramref name="member"/>.</returns>
        public static bool Contains(this ICollection<EndPoint> endpoints, IClusterMember member)
            => endpoints.Contains(member.Endpoint);

        /// <summary>
        /// Determines whether the collection of end points contains the end point of the cluster member.
        /// </summary>
        /// <param name="endpoints">The collection of end points.</param>
        /// <param name="member">The cluster member.</param>
        /// <returns><see langword="true"/> if <paramref name="endpoints"/> contains <see cref="IClusterMember.Endpoint"/> if <paramref name="member"/>.</returns>
        public static bool Contains(this ICollection<IPEndPoint> endpoints, IClusterMember member)
            => endpoints.Contains(member.Endpoint);

        /// <summary>
        /// Indicates that the network endpoint has the same address as the cluster member.
        /// </summary>
        /// <param name="endpoint">The network end point.</param>
        /// <param name="member">The cluster member.</param>
        /// <returns><see langword="true"/> if <paramref name="endpoint"/> is equal to <see cref="IClusterMember.Endpoint"/> property of <paramref name="member"/>.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [CLSCompliant(false)]
        public static bool Represents(this EndPoint endpoint, IClusterMember member)
            => member.Endpoint.Equals(endpoint);
    }
}