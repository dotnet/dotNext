using System;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster that supports dynamic addition or removal of cluster members.
    /// </summary>
    public interface IExpandableCluster : ICluster
    {
        /// <summary>
        /// An event raised when new cluster member is detected.
        /// </summary>
        event ClusterChangedEventHandler MemberAdded;

        /// <summary>
        /// An event raised when cluster member is removed gracefully.
        /// </summary>
        event ClusterChangedEventHandler MemberRemoved;

        /// <summary>
        /// Gracefully disconnects the entire application from the cluster. 
        /// </summary>
        Task DisconnectAsync();
    }
}