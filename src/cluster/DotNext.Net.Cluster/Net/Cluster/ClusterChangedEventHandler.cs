using System;
using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents handler of the event occurred when the member is added or remove to/from the cluster.
    /// </summary>
    /// <param name="cluster">The cluster of nodes.</param>
    /// <param name="member">The member that is added or removed.</param>
    public delegate void ClusterChangedEventHandler(ICluster cluster, IClusterMember member);

    internal static class PeerMeshEventsBridge
    {
        internal static void Invoke(this EventHandler<EndPoint> handler, ICluster cluster, IClusterMember member)
            => handler(cluster, member.EndPoint);
    }
}