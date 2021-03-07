using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Allows to override discovery logic of cluster members.
    /// </summary>
    public interface IMemberDiscoveryService
    {
        // TODO: IReadOnlyCollection<Uri> should be replaced with IReadOnlySet<Uri> in .NET 6

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
        /// <param name="token">The token that can be used to cancel watching.</param>
        /// <returns>The task representing long-running operation.</returns>
        /// <remarks>
        /// This method should never throw <see cref="OperationCanceledException"/> exception.
        /// If watching is canceled then task turns into completed state.
        /// </remarks>
        Task WatchAsync(Func<IReadOnlyCollection<Uri>, CancellationToken, Task> callback, CancellationToken token);
    }
}