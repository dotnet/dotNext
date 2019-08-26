using DotNext.Net.Cluster.Messaging;
using DotNext.Net.Cluster.Replication;
using DotNext.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public sealed class InMemoryAuditTrailTests : Assert
    {
        private sealed class LogEntry : TextMessage, ILogEntry
        {
            public LogEntry(string command)
                : base(command, "Entry")
            {
            }

            public long Term { get; set; }
        }

        [Fact]
        public static async Task RaftPersistentState()
        {
            IPersistentState auditTrail = new InMemoryAuditTrail();
            NotNull(auditTrail.First);
            Equal(0, auditTrail.First.Term);
            await auditTrail.UpdateTermAsync(10);
            Equal(10, auditTrail.Term);
            await auditTrail.IncrementTermAsync();
            Equal(11, auditTrail.Term);
        }

        private sealed class CommitDetector : AsyncManualResetEvent
        {
            internal long Count;

            internal CommitDetector()
                : base(false)
            {
            }

            internal Task OnCommitted(IAuditTrail<ILogEntry> sender, long startIndex, long count)
            {
                Count = count;
                Set();
                return Task.CompletedTask;
            }
        }

        [Fact]
        public static async Task LogCompaction()
        {
            IPersistentState auditTrail = new InMemoryAuditTrail();
            var entry1 = new LogEntry("SET X=0") { Term = 1 };
            var entry2 = new LogEntry("SET Y=0") { Term = 2 };
            var entry3 = new LogEntry("SET Z=0") { Term = 5 };
            Equal(1L, await auditTrail.AppendAsync(new[] { entry1, entry2, entry3 }));
            Equal(0L, await auditTrail.ForceCompactionAsync());
            Equal(2L, await auditTrail.CommitAsync(1L, 2L));
            Equal(2L, auditTrail.GetLastIndex(true));
            Equal(2L, await auditTrail.ForceCompactionAsync());
            Equal(2L, auditTrail.GetLastIndex(true));
            Equal(3L, auditTrail.GetLastIndex(false));
            Equal(5L, (await auditTrail.GetEntriesAsync(3L))[0].Term);
            var entry4 = new LogEntry("SET H=0") { Term = 7 };
            Equal(4L, await auditTrail.AppendAsync(new[] { entry4 }));
            Equal(2L, auditTrail.GetLastIndex(true));
            Equal(4L, auditTrail.GetLastIndex(false));
            Equal(2L, await auditTrail.CommitAsync(3L));
            Equal(4L, auditTrail.GetLastIndex(true));
            Equal(2L, await auditTrail.ForceCompactionAsync());
        }

        [Fact]
        public static async Task Appending()
        {
            IPersistentState auditTrail = new InMemoryAuditTrail();
            Equal(0, auditTrail.GetLastIndex(false));
            Equal(0, auditTrail.GetLastIndex(true));
            var entry1 = new LogEntry("SET X=0") { Term = 1 };
            var entry2 = new LogEntry("SET Y=0") { Term = 2 };
            Equal(1, await auditTrail.AppendAsync(new[] { entry1, entry2 }));
            Equal(0, auditTrail.GetLastIndex(true));
            Equal(2, auditTrail.GetLastIndex(false));
            var entries = await auditTrail.GetEntriesAsync(1, 2);
            Equal(2, entries.Count);
            entry1 = (LogEntry)entries[0];
            entry2 = (LogEntry)entries[1];
            Equal("SET X=0", entry1.Content);
            Equal("SET Y=0", entry2.Content);
            //now replace entry at index 2 with new entry
            entry2 = new LogEntry("ADD") { Term = 3 };
            Equal(2, await auditTrail.AppendAsync(new[] { entry2 }, 2));
            entries = await auditTrail.GetEntriesAsync(1, 2);
            Equal(2, entries.Count);
            entry1 = (LogEntry)entries[0];
            entry2 = (LogEntry)entries[1];
            Equal("SET X=0", entry1.Content);
            Equal("ADD", entry2.Content);
            Equal(2, auditTrail.GetLastIndex(false));
            Equal(0, auditTrail.GetLastIndex(true));
            //commit all entries
            using (var detector = new CommitDetector())
            {
                auditTrail.Committed += detector.OnCommitted;
                Equal(2, await auditTrail.CommitAsync(1));
                await detector.Wait();
                Equal(2, auditTrail.GetLastIndex(true));
                Equal(2, detector.Count);
            }
        }


    }
}
