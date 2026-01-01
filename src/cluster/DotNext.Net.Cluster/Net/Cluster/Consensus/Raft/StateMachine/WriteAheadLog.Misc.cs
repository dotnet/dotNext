namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    /// <summary>
    /// Imports log entries from another WAL.
    /// </summary>
    /// <remarks>
    /// This method is intended for migration purposes only, it should not be used
    /// during the normal operation.
    /// </remarks>
    /// <param name="other">The source of log entries.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task ImportAsync(WriteAheadLog other, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(other);

        using (var reader = other.CreateReader(LastCommittedEntryIndex + 1L, other.LastEntryIndex))
        {
            foreach (var entry in reader)
            {
                await AppendAsync(entry, entry.Index, token).ConfigureAwait(false);
            }
        }

        await CommitAsync(other.LastCommittedEntryIndex, token).ConfigureAwait(false);
        await WaitForApplyAsync(other.LastAppliedIndex, token).ConfigureAwait(false);
        await FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// Represents catastrophic WAL failure.
    /// </summary>
    public abstract class IntegrityException : Exception
    {
        private protected IntegrityException(string message)
            : base(message)
        {
        }
    }
}