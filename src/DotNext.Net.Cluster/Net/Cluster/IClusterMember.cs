using System;
using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster member.
    /// </summary>
    public interface IClusterMember
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
        /// Gets unique identifier of the current node in the cluster.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets optional name of the current node in the cluster.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates that this member is available through the network.
        /// </summary>
        bool IsAvailable { get; }
    }
}
