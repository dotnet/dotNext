using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    internal static class AuditTrail
    {
        internal static async ValueTask<bool> IsUpToDateAsync(this IAuditTrail<ILogEntry> auditTrail, long index, long term)
        {
            var localIndex = auditTrail.GetLastIndex(false);
            var localTerm = (await auditTrail.GetEntryAsync(localIndex).ConfigureAwait(false) ?? auditTrail.First)
                .Term;
            return index >= localIndex && term >= localTerm;
        }

        internal static async ValueTask<bool> ContainsAsync(this IAuditTrail<ILogEntry> auditTrail, long index,
            long term)
            => term == (await auditTrail.GetEntryAsync(index).ConfigureAwait(false) ?? auditTrail.First).Term;
    }
}
