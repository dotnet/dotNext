using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using Messaging;

    /// <summary>
    /// Represents data checkpoint.
    /// </summary>
    public interface ICheckpoint
    {
        /// <summary>
        /// Creates replication message that contains all entries between this checkpoint
        /// and the current state of data source.
        /// </summary>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The replication message; <see langword="null"/> if there is no change detected between this checkpoint and the current state of data source.</returns>
        Task<IMessage> CreateReplicaMessageAsync(CancellationToken token);
    }
}