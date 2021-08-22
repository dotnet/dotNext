using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents a feature of <see cref="IPersistentState"/>
    /// that allows to store cluster membership directly in the log.
    /// </summary>
    public interface IClusterConfigurationStorage : IPersistentState
    {
        /// <summary>
        /// Gets or sets tracker of membership changes in the underlying storage.
        /// </summary>
        Func<ReadOnlyMemory<byte>, bool, IBufferWriter<byte>, ValueTask>? MembershipTracker { get; set; }

        /// <summary>
        /// Loads information about all cluster members from the storage.
        /// </summary>
        /// <param name="loader">The reader of the configuration.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        ValueTask LoadConfigurationAsync(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> loader, CancellationToken token = default);
    }
}