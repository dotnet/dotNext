using System;

namespace DotNext.Net.Cluster.Replication
{
    internal static class AuditTrail
    {
        internal static ILogEntry<EntryId> GetPrevious<EntryId>(this IAuditTrail<EntryId> log, ILogEntry<EntryId> entry)
            where EntryId : struct, IEquatable<EntryId>
            => log[log.GetPrevious(entry.Id)];

        internal static ILogEntry<EntryId> GetNext<EntryId>(this IAuditTrail<EntryId> log, ILogEntry<EntryId> entry)
            where EntryId : struct, IEquatable<EntryId>
        {
            var next = log.GetNext(entry.Id);
            return next.TryGet(out var id) ? log[id] : null;
        }
    }
}
