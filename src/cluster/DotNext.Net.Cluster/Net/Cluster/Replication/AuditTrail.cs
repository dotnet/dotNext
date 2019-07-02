using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;

    internal static class AuditTrail
    {
        internal static async ValueTask<LogEntry> GetEntryAsync<LogEntry>(this IAuditTrail<LogEntry> auditTrail, long index)
            where LogEntry : class, IMessage

        {
            var entries = await auditTrail.GetEntriesAsync(index, index).ConfigureAwait(false);
            return entries.Length > 0 ? entries.Span[0] : null;
        }
    }
}
