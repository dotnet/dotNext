using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents handler of the event occurred when transaction log asked to commit a number
    /// of entries.
    /// </summary>
    /// <param name="sender">The transaction log.</param>
    /// <param name="startIndex">The index of the first committed log entry in the transaction log.</param>
    /// <param name="count">The number of committed entries.</param>
    /// <typeparam name="LogEntry">The type of the log entries stored in transaction log.</typeparam>
    public delegate Task CommitEventHandler<LogEntry>(IAuditTrail<LogEntry> sender, long startIndex, long count)
        where LogEntry : class, ILogEntry;
}