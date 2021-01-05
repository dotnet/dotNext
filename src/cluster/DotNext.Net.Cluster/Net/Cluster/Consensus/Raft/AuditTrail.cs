using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;

    /// <summary>
    /// Represents various extensions for <see cref="IPersistentState"/> interface.
    /// </summary>
    public static class AuditTrail
    {
        private sealed class TermReader : ILogEntryConsumer<IRaftLogEntry, long>
        {
            internal static readonly ILogEntryConsumer<IRaftLogEntry, long> Instance = new TermReader();

            private TermReader()
            {
            }

            ValueTask<long> ILogEntryConsumer<IRaftLogEntry, long>.ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                => new ValueTask<long>(entries[0].Term);
        }

        internal static ValueTask<long> GetTermAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, CancellationToken token)
            => auditTrail.ReadAsync<long>(TermReader.Instance, index, index, token);

        internal static async ValueTask<bool> IsUpToDateAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
        {
            var localIndex = auditTrail.GetLastIndex(false);
            return index >= localIndex && term >= await auditTrail.GetTermAsync(localIndex, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks whether the log entry at the specified index has the specified term.
        /// </summary>
        /// <param name="auditTrail">The audit trail with Raft-specific log entries.</param>
        /// <param name="index">The index of the log entry to check.</param>
        /// <param name="term">The expected Term value.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if the log entry at index <paramref name="index"/> has the term equal to <paramref name="term"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<bool> ContainsAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
            => index <= auditTrail.GetLastIndex(false) && term == await auditTrail.GetTermAsync(index, token).ConfigureAwait(false);

        internal static ValueTask<long> AppendNoOpEntry(this IPersistentState auditTrail, CancellationToken token)
            => auditTrail.AppendAsync(new EmptyLogEntry(auditTrail.Term), token);
    }
}
