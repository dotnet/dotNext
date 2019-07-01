using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;

    internal static class AuditTrail
    {
        internal static async ValueTask<LogEntry> GetEntryAsync<LogEntry>(this IAuditTrail<LogEntry> auditTrail, long index)
            where LogEntry : class, IMessage

        {
            var collection = await auditTrail.GetEntriesAsync(index, index).ConfigureAwait(false);
            return collection.Count > 0 ? collection[0] : null;
        }
    }
}
