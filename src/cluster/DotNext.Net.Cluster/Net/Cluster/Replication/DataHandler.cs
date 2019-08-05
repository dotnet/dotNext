using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;

    /// <summary>
    /// Represents data handler that performs data-centric operations
    /// and produce change set in the form of entries.
    /// </summary>
    /// <typeparam name="T">The type of the handler parameter.</typeparam>
    /// <typeparam name="LogEntry">The type of the transaction log entry.</typeparam>
    /// <returns>The collection of log entries.</returns>
    public delegate ValueTask<IReadOnlyList<LogEntry>> DataHandler<in T, LogEntry>(T input)
        where LogEntry : class, IMessage;
}
