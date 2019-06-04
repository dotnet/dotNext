using System;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public interface IClusterMemberConfiguration
    {
        bool AbsoluteMajority { get; }

        TimeSpan ElectionTimeout { get; }

        TimeSpan MessageProcessingTimeout { get; }
    }
}
