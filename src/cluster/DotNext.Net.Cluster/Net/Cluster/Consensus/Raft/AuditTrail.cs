namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;

internal static class AuditTrail
{
    private sealed class TermReader : ILogEntryConsumer<IRaftLogEntry, long>
    {
        internal static readonly ILogEntryConsumer<IRaftLogEntry, long> Instance = new TermReader();

        private TermReader()
        {
        }

        ValueTask<long> ILogEntryConsumer<IRaftLogEntry, long>.ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            => new(entries[0].Term);

        bool ILogEntryConsumer<IRaftLogEntry, long>.LogEntryMetadataOnly => true;
    }

    internal static ValueTask<long> GetTermAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, CancellationToken token)
        => index == 0L ? new(0L) : auditTrail.ReadAsync<long>(TermReader.Instance, index, index, token);

    internal static async ValueTask<bool> IsUpToDateAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
    {
        var localIndex = auditTrail.LastUncommittedEntryIndex;
        return index >= localIndex && term >= await auditTrail.GetTermAsync(localIndex, token).ConfigureAwait(false);
    }

    internal static async ValueTask<bool> ContainsAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
        => index <= auditTrail.LastUncommittedEntryIndex && term == await auditTrail.GetTermAsync(index, token).ConfigureAwait(false);

    internal static ValueTask<long> AppendNoOpEntry(this IPersistentState auditTrail, CancellationToken token)
        => auditTrail.AppendAsync(new EmptyLogEntry(auditTrail.Term), token);
}