using System;
using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster that supports dynamic addition or removal of cluster members.
    /// </summary>
    public interface IExpandableCluster : ICluster // TODO: Remove in .NEXT 4 (replaced with IPeerMesh events)
    {
        /// <summary>
        /// An event raised when new cluster member is detected.
        /// </summary>
        event ClusterChangedEventHandler MemberAdded;

        /// <summary>
        /// An event raised when cluster member is removed gracefully.
        /// </summary>
        event ClusterChangedEventHandler MemberRemoved;

        /// <inheritdoc/>
        event EventHandler<EndPoint> IPeerMesh.PeerDiscovered
        {
            add => MemberAdded += value.Invoke;
            remove => MemberAdded -= value.Invoke;
        }

        /// <inheritdoc/>
        event EventHandler<EndPoint> IPeerMesh.PeerGone
        {
            add => MemberRemoved += value.Invoke;
            remove => MemberRemoved -= value.Invoke;
        }
    }
}