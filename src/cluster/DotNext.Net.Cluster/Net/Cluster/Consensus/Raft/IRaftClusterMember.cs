﻿using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Membership;

/// <summary>
/// Represents cluster member accessible through Raft protocol.
/// </summary>
public interface IRaftClusterMember : IClusterMember
{
    /// <summary>
    /// Represents metrics attribute containing the address of the local node.
    /// </summary>
    protected const string RemoteAddressMeterAttributeName = "dotnext.raft.client.address";

    /// <summary>
    /// Represents metrics attribute containing Raft message type.
    /// </summary>
    protected const string MessageTypeAttributeName = "dotnext.raft.client.message";

    /// <summary>
    /// Requests vote from the member.
    /// </summary>
    /// <param name="term">Term value maintained by local cluster member.</param>
    /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
    /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>Vote received from the member; <see langword="true"/> if node accepts new leader, <see langword="false"/> if node doesn't accept new leader.</returns>
    Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    /// <summary>
    /// Checks whether the transition to Candidate state makes sense.
    /// </summary>
    /// <remarks>
    /// Called by a server before changing itself to Candidate status.
    /// If a majority of servers return true, proceed to Candidate.
    /// Otherwise, wait for another election timeout.
    /// </remarks>
    /// <param name="term">Term value maintained by local cluster member.</param>
    /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
    /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>Pre-vote result received from the member.</returns>
    /// <seealso href="https://www.openlife.cc/sites/default/files/4-modifications-for-Raft-consensus.pdf">Four modifications for the Raft consensus algorithm.</seealso>
    Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled<Result<PreVoteResult>>(token) : Task.FromResult<Result<PreVoteResult>>(new() { Term = term, Value = PreVoteResult.Accepted });

    /// <summary>
    /// Transfers log entries to the member.
    /// </summary>
    /// <typeparam name="TEntry">The type of the log entry.</typeparam>
    /// <typeparam name="TList">The type of the log entries list.</typeparam>
    /// <param name="term">Term value maintained by local cluster member.</param>
    /// <param name="entries">A set of entries to be replicated with this node.</param>
    /// <param name="prevLogIndex">Index of log entry immediately preceding new ones.</param>
    /// <param name="prevLogTerm">Term of <paramref name="prevLogIndex"/> entry.</param>
    /// <param name="commitIndex">Last entry known to be committed by the local node.</param>
    /// <param name="config">The list of cluster members.</param>
    /// <param name="applyConfig">
    /// <see langword="true"/> to inform that the receiver must apply previously proposed configuration;
    /// <see langword="false"/> to propose a new configuration.
    /// </param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The processing result.</returns>
    /// <exception cref="MemberUnavailableException">The member is unreachable through the network.</exception>
    Task<Result<HeartbeatResult>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>;

    /// <summary>
    /// Installs the snapshot of the log to this cluster member.
    /// </summary>
    /// <param name="term">Leader's term.</param>
    /// <param name="snapshot">The log entry representing the snapshot.</param>
    /// <param name="snapshotIndex">The index of the last included log entry in the snapshot.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The processing result.</returns>
    /// <exception cref="MemberUnavailableException">The member is unreachable through the network.</exception>
    Task<Result<HeartbeatResult>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token);

    /// <summary>
    /// Starts a new round of heartbeats.
    /// </summary>
    /// <param name="commitIndex">The index of the last committed log entry.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the last committed log entry;
    /// or <see langword="null"/> if the member is not a leader.
    /// </returns>
    /// <exception cref="MemberUnavailableException">The member is unreachable through the network.</exception>
    Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token);

    /// <summary>
    /// Gets a reference to the replication state.
    /// </summary>
    /// <remarks>
    /// Implementing class should provide memory storage for <see cref="ReplicationState"/> type without
    /// any special semantics.
    /// </remarks>
    protected internal ref ReplicationState State { get; }

    /// <summary>
    /// Aborts all active outbound requests asynchronously.
    /// </summary>
    /// <returns>The task representing shutdown operation.</returns>
    ValueTask CancelPendingRequestsAsync();

    /// <summary>
    /// Represents replication state of the member used internally by Raft implementation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    protected internal struct ReplicationState
    {
        /// <summary>
        /// Gets or sets replication index of the member.
        /// </summary>
        public long NextIndex;

        /// <summary>
        /// Gets or sets configuration fingerprint associated with the member.
        /// </summary>
        public long ConfigurationFingerprint;

        internal readonly long PrecedingIndex
        {
            get
            {
                var result = NextIndex;
                if (result > 0L)
                    result -= 1L;

                return result;
            }
        }
    }
}