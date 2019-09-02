using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using Threading;

    internal sealed class CommitEvent<LogEntry> : AsyncManualResetEvent
        where LogEntry : class, ILogEntry
    {
        private readonly long expectedIndex;

        internal CommitEvent(long expectedIndex) : base(false) => this.expectedIndex = expectedIndex;

        private void SetIfCommitted(IAuditTrail<LogEntry> auditTrail)
        {
            if (auditTrail.GetLastIndex(true) >= expectedIndex)
            {
                Set();
                DetachFrom(auditTrail);
            }
        }

        private Task OnCommitted(IAuditTrail<LogEntry> sender, long startIndex, long count)
        {
            SetIfCommitted(sender);
            return Task.CompletedTask;
        }

        internal void AttachTo(IAuditTrail<LogEntry> auditTrail)
        {
            auditTrail.Committed += OnCommitted;
            SetIfCommitted(auditTrail);
        }

        internal void DetachFrom(IAuditTrail<LogEntry> auditTrail)
            => auditTrail.Committed -= OnCommitted;
    }
}
