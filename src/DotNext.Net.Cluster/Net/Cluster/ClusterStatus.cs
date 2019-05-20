namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster status.
    /// </summary>
    public enum ClusterStatus
    {
        /// <summary>
        /// Indicates that there is no consensus about cluster state.
        /// </summary>
        NoConsensus = 0,

        /// <summary>
        /// Cluster is operating normally.
        /// </summary>
        Operating,
    }
}
