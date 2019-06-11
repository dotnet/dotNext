using DotNext.Net.Cluster.Consensus.Raft;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public interface IClusterMemberConfiguration
    {
        bool AbsoluteMajority { get; }

        /// <summary>
        /// Gets leader election timeout settings.
        /// </summary>
        ElectionTimeout ElectionTimeout { get; }

        bool IsVotedFor(IRaftClusterMember member);
    }
}
