using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    internal static class AuditTrail
    {
        internal static async Task<long> GetTermAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, CancellationToken token)
        {
            using (var entries = await auditTrail.GetEntriesAsync(index, index, token).ConfigureAwait(false))
                return entries[0].Term;
        }

        internal static async Task<bool> IsUpToDateAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
        {
            var localIndex = auditTrail.GetLastIndex(false);
            return index >= localIndex && term >= await auditTrail.GetTermAsync(localIndex, token).ConfigureAwait(false);
        }

        internal static async Task<bool> ContainsAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
            => index <= auditTrail.GetLastIndex(false) && term == await auditTrail.GetTermAsync(index, token).ConfigureAwait(false);
    }
}
