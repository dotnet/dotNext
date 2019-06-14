using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    /// <summary>
    /// Represents persistent state of local cluster member
    /// required by Raft consensus protocol.
    /// </summary>
    public interface IPersistentState : IAuditTrail<LogEntryId>
    {
        /// <summary>
        /// Determines whether the local member granted its vote for the specified remote member.
        /// </summary>
        /// <param name="member">The cluster member to check.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns><see langword="true"/> if the local member granted its vote for the specified remote member; otherwise, <see langword="false"/>.</returns>
        Task<bool> IsVotedForAsync(IRaftClusterMember member, CancellationToken token);

        /// <summary>
        /// Reads Term value associated with the local member
        /// from the persistent storage.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The term restored from persistent storage.</returns>
        Task<long> RestoreTermAsync(CancellationToken token);

        /// <summary>
        /// Persists the last actual Term.
        /// </summary>
        /// <param name="term">The term value to be persisted.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        Task SaveTermAsync(long term, CancellationToken token);

        /// <summary>
        /// Persists the item that was voted for on in the last vote.
        /// </summary>
        /// <param name="member">The member which identifier should be stored inside of persistence storage. May be <see langword="null"/>.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        Task SaveVotedForAsync(IRaftClusterMember member, CancellationToken token);
    }
}
