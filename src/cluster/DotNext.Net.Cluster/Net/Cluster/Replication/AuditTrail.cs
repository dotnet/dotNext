using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    internal static class AuditTrail
    {
        internal static async ValueTask<LogEntry> GetEntryAsync<LogEntry>(this IAuditTrail<LogEntry> auditTrail, long index)
            where LogEntry : class, ILogEntry

        {
            var entries = await auditTrail.GetEntriesAsync(index, index).ConfigureAwait(false);
            return entries.Count > 0 ? entries[0] : null;
        }
    }
}
