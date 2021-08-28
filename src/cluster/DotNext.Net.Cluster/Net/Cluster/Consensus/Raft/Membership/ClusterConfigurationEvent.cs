namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    /// <summary>
    /// Represents an event related to changes in cluster configuration.
    /// </summary>
    /// <typeparam name="TAddress"></typeparam>
    public readonly struct ClusterConfigurationEvent<TAddress>
        where TAddress : notnull
    {
        /// <summary>
        /// Gets identifier of the cluster member.
        /// </summary>
        public ClusterMemberId Id { get; init; } // TODO: Must be required in C# 10

        /// <summary>
        /// Gets the address of the cluster member.
        /// </summary>
        public TAddress Address { get; init; }

        /// <summary>
        /// Gets a value indicating that the member is added to or removed from the configuration.
        /// </summary>
        public bool IsAdded { get; init; }
    }
}