using System.Threading;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;
    using Threading;

    internal sealed class CommitEvent<LogEntry> : AsyncManualResetEvent
        where LogEntry : class, IMessage
    {
        private readonly long expectedIndex;

        internal CommitEvent(long expectedIndex) : base(false) => this.expectedIndex = expectedIndex;

        private void SetIfCommitted(IAuditTrail<LogEntry> auditTrail)
        {
            if(auditTrail.GetLastIndex(true) >= expectedIndex)
            {
                Set();
                DetachFrom(auditTrail);
            }
        }

        private void OnCommitted(IAuditTrail<LogEntry> sender, long startIndex, long count)
            => SetIfCommitted(sender);

        internal void AttachTo(IAuditTrail<LogEntry> auditTrail)
        {
            auditTrail.Committed += OnCommitted;
            SetIfCommitted(auditTrail);
        }

        internal void DetachFrom(IAuditTrail<LogEntry> auditTrail)
            => auditTrail.Committed -= OnCommitted;
    }
}
