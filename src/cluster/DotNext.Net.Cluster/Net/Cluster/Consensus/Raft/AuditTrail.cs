using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    internal static class AuditTrail
    {
        internal static async Task<long> GetTermAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index)
        {
            using (var entries = await auditTrail.GetEntriesAsync(index, index).ConfigureAwait(false))
                return entries[0].Term;
        }

        internal static async Task<bool> IsUpToDateAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term)
        {
            var localIndex = auditTrail.GetLastIndex(false);
            long localTerm = await auditTrail.GetTermAsync(localIndex).ConfigureAwait(false);
            return index >= localIndex && term >= localTerm;
        }

        internal static async Task<bool> ContainsAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term)
            => index <= auditTrail.GetLastIndex(false) && term == await auditTrail.GetTermAsync(index).ConfigureAwait(false);
    }
}
