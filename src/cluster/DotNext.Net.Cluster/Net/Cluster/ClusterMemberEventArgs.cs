using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents arguments for all events related to cluster members.
    /// </summary>
    public class ClusterMemberEventArgs : PeerEventArgs
    {
        /// <summary>
        /// Initializes a new container for the event arguments.
        /// </summary>
        /// <param name="member">The cluster member.</param>
        public ClusterMemberEventArgs(IClusterMember member)
            => Member = member;

        /// <summary>
        /// Gets cluster member.
        /// </summary>
        public IClusterMember Member { get; }

        /// <summary>
        /// Gets the address of the cluster member.
        /// </summary>
        public sealed override EndPoint PeerAddress => Member.EndPoint;
    }
}