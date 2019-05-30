using System;
using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster member.
    /// </summary>
    public interface IClusterMember : IClusterMemberIdentity
    {
        /// <summary>
        /// Represents cluster member endpoint.
        /// </summary>
        IPEndPoint Endpoint { get; }

        /// <summary>
        /// Indicates that executing host is a leader node in the cluster.
        /// </summary>
        bool IsLeader { get; }

        /// <summary>
        /// Indicates that this instance represents remote or local cluster member.
        /// </summary>
        bool IsRemote { get; }

        /// <summary>
        /// Gets status of this member.
        /// </summary>
        ClusterMemberStatus Status { get; }
    }
}
