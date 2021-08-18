using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Provides additional control over <see cref="IRaftCluster"/> lifecycle.
    /// </summary>
    public interface IClusterMemberLifetime
    {
        /// <summary>
        /// Configures instance of <see cref="IRaftCluster"/> after its construction.
        /// </summary>
        /// <remarks>
        /// This method can be used to attach all necessary event handlers.
        /// </remarks>
        /// <param name="cluster">The instance to be configured.</param>
        /// <param name="metadata">The metadata of the local cluster member to fill.</param>
        void Initialize(IRaftCluster cluster, IDictionary<string, string> metadata); // TODO: Rename to OnStart

        /// <summary>
        /// Configures instance of <see cref="IRaftCluster"/> before its destruction.
        /// </summary>
        /// <remarks>
        /// This method can be used to detach all event handlers attached in <see cref="Initialize"/> method.
        /// </remarks>
        /// <param name="cluster">The instance to be configured.</param>
        void Shutdown(IRaftCluster cluster); // TODO: Rename to OnStop

        /// <summary>
        /// Gets predicate that can be used to override default logic for searching of local cluster member.
        /// </summary>
        Func<IRaftClusterMember, CancellationToken, ValueTask<bool>>? LocalMemberSelector => null;
    }
}