namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Provides additional control over <see cref="IRaftCluster"/> lifecycle.
    /// </summary>
    public interface IRaftClusterConfigurer
    {
        /// <summary>
        /// Configures instance of <see cref="IRaftCluster"/> after its construction.
        /// </summary>
        /// <remarks>
        /// This method can be used to attach all necessary event handlers.
        /// </remarks>
        /// <param name="cluster">The instance to be configured.</param>
        void Initialize(IRaftCluster cluster);

        /// <summary>
        /// Configures instance of <see cref="IRaftCluster"/> before its destruction.
        /// </summary>
        /// <remarks>
        /// This method can be used to detach all event handlers attached in <see cref="Initialize"/> method.
        /// </remarks>
        /// <param name="cluster">The instance to be configured.</param>
        void Cleanup(IRaftCluster cluster);
    }
}