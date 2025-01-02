namespace DotNext.Net.Cluster.Consensus.Raft;

using Runtime;
using BoxedClusterMemberId = Runtime.BoxedValue<ClusterMemberId>;

/// <summary>
/// Represents persistent state of local cluster member
/// required by Raft consensus protocol.
/// </summary>
public interface IPersistentState : IO.Log.IAuditTrail<IRaftLogEntry>
{
    /// <summary>
    /// Determines whether the local member granted its vote for the specified remote member.
    /// </summary>
    /// <param name="id">The cluster member to check.</param>
    /// <returns><see langword="true"/> if the local member granted its vote for the specified remote member; otherwise, <see langword="false"/>.</returns>
    bool IsVotedFor(in ClusterMemberId id);

    /// <summary>
    /// Reads Term value associated with the local member
    /// from the persistent storage.
    /// </summary>
    /// <returns>The term restored from persistent storage.</returns>
    long Term { get; }

    /// <summary>
    /// Increments <see cref="Term"/> value and persists the item that was voted for on in the last vote.
    /// </summary>
    /// <param name="member">The member which identifier should be stored inside of persistence storage. May be <see langword="null"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The updated <see cref="Term"/> value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask<long> IncrementTermAsync(ClusterMemberId member, CancellationToken token = default);

    /// <summary>
    /// Persists the last actual Term.
    /// </summary>
    /// <param name="term">The term value to be persisted.</param>
    /// <param name="resetLastVote">
    /// <see langword="true"/> to reset information about the last vote;
    /// <see langword="false"/> to keep information about the last vote unchanged.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask UpdateTermAsync(long term, bool resetLastVote, CancellationToken token = default);

    /// <summary>
    /// Persists the item that was voted for on in the last vote.
    /// </summary>
    /// <param name="member">The member which identifier should be stored inside of persistence storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of the asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask UpdateVotedForAsync(ClusterMemberId member, CancellationToken token = default);

    internal static bool IsVotedFor(BoxedClusterMemberId? lastVote, in ClusterMemberId expected)
        => lastVote is null || lastVote.Unbox() == expected;
}