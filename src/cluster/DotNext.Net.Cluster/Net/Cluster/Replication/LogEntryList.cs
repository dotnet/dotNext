using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DotNext.Net.Cluster.Replication
{
    internal class LogEntryList<LogEntry> : ReadOnlyCollection<LogEntry>, ILogEntryList<LogEntry>
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
}
