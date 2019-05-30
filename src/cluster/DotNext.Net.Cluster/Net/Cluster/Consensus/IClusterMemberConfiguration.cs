using System;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public interface IClusterMemberConfiguration
    {
        /// <summary>
        /// Gets name of the cluster member.
        /// </summary>
        string MemberName { get; }

        bool AbsoluteMajority { get; }

        TimeSpan ElectionTimeout { get; }

        TimeSpan MessageProcessingTimeout { get; }
    }
}
