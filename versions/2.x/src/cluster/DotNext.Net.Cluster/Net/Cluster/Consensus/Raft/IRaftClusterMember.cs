using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents cluster member accessible through Raft protocol.
    /// </summary>
    public interface IRaftClusterMember : IClusterMember
    {
        /// <summary>
        /// Requests vote from the member.
        /// </summary>
        /// <param name="term">Term value maintained by local cluster member.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>Vote received from member; <see langword="true"/> if node accepts new leader, <see langword="false"/> if node doesn't accept new leader, <see langword="null"/> if node is not available.</returns>
        Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        /// <summary>
        /// Transfers transaction log entry to the member.
        /// </summary>
        /// <typeparam name="TEntry">The type of the log entry.</typeparam>
        /// <typeparam name="TList">The type of the log entries list.</typeparam>
        /// <param name="term">Term value maintained by local cluster member.</param>
        /// <param name="entries">A set of entries to be replicated with this node.</param>
        /// <param name="prevLogIndex">Index of log entry immediately preceding new ones.</param>
        /// <param name="prevLogTerm">Term of <paramref name="prevLogIndex"/> entry.</param>
        /// <param name="commitIndex">Last entry known to be committed by the local node.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns><see langword="true"/> if message is handled successfully by this member; <see langword="false"/> if message is rejected due to invalid Term/Index number.</returns>
        /// <exception cref="MemberUnavailableException">The member is unreachable through network.</exception>
        Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : IRaftLogEntry
            where TList : IReadOnlyList<TEntry>;

        /// <summary>
        /// Installs the snapshot of the log to this cluster member.
        /// </summary>
        /// <param name="term">Leader's term.</param>
        /// <param name="snapshot">The log entry representing the snapshot.</param>
        /// <param name="snapshotIndex">The index of the last included log entry in the snapshot.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns><see langword="true"/> if snapshot is installed successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="MemberUnavailableException">The member is unreachable through network.</exception>
        Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token);

        /// <summary>
        /// Index of next log entry to send to this node.
        /// </summary>
        ref long NextIndex { get; }

        /// <summary>
        /// Aborts all active outbound requests.
        /// </summary>
        [Obsolete("Use CancelPendingRequestsAsync method instead")]
        void CancelPendingRequests()
        {
        }

        /// <summary>
        /// Aborts all active outbound requests asynchronously.
        /// </summary>
        /// <returns>The task representing shutdown operation.</returns>
        ValueTask CancelPendingRequestsAsync()
            => new ValueTask(Task.Factory.StartNew(CancelPendingRequests, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current));
    }
}
