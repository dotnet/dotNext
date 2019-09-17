using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    internal class LogEntryList<LogEntry> : ReadOnlyCollection<LogEntry>, IAuditTrailSegment<LogEntry>
        where LogEntry : class, ILogEntry
    {
        internal LogEntryList(IList<LogEntry> entries)
            : base(entries)
        {
        }

        internal LogEntryList()
            : this(Array.Empty<LogEntry>())
        {
        }

        void IDisposable.Dispose() { }
    }

    //TODO: Should be removed in .NET Standard 2.1
    internal static class LogEntryEnumerator
    {
        internal static ValueTask<LogEntry> Advance<LogEntry>(this IEnumerator<LogEntry> entry) where LogEntry : class, ILogEntry => new ValueTask<LogEntry>(entry.MoveNext() ? entry.Current : null);
    }
}
