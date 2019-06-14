namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public interface IClusterMemberConfiguration
    {
        /// <summary>
        /// Indicates that votes of unavailable cluster members are
        /// taken into account during voting process.
        /// </summary>
        /// <remarks>
        /// <see langword="true"/> value allows to build CA distributed cluster
        /// while <see langword="false"/> value allows to build CP/AP distributed cluster. 
        /// </remarks>
        bool AbsoluteMajority { get; }

        /// <summary>
        /// Gets leader election timeout settings.
        /// </summary>
        ElectionTimeout ElectionTimeout { get; }
    }
}
