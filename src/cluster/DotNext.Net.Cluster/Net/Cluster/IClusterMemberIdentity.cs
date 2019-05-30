using System;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents identity of the cluster member.
    /// </summary>
    public interface IClusterMemberIdentity
    {
        /// <summary>
        /// Gets unique identifier of the current node in the cluster.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets optional name of the current node in the cluster.
        /// </summary>
        string Name { get; }
    }
}
