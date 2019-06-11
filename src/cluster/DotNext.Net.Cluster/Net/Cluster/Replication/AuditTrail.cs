namespace DotNext.Net.Cluster.Replication
{
    internal static class AuditTrail
    {
        internal static ILogEntry GetPrevious(this IAuditTrail log, ILogEntry entry)
            => log[log.GetPrevious(entry.Id)];

        internal static ILogEntry GetNext(this IAuditTrail log, ILogEntry entry)
        {
            var next = log.GetNext(entry.Id);
            return next.TryGet(out var id) ? log[id] : null;
        }
    }
}
