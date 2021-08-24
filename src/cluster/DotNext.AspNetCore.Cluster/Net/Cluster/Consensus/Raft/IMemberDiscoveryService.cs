using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Allows to override discovery logic of cluster members.
    /// </summary>
    [Obsolete("Use appropriate ClusterMemberBootstrap mode in production")]
    public interface IMemberDiscoveryService
    {
        /// <summary>
        /// Discover members asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <remarks>
        /// This method is used only once at boot time.
        /// </remarks>
        /// <returns>A set of cluster members.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask<IReadOnlyCollection<Uri>> DiscoverAsync(CancellationToken token);

        /// <summary>
        /// Creates a watcher with the specified callback used to report a new list of cluster members.
        /// </summary>
        /// <param name="callback">The callback used to report about membership changes.</param>
        /// <param name="token">The token that can be used to cancel initialization of watcher.</param>
        /// <returns>The object that can be used to cancel watching.</returns>
        ValueTask<IDisposable> WatchAsync(Func<IReadOnlyCollection<Uri>, CancellationToken, Task> callback, CancellationToken token);
    }
}