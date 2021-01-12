using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using LogEntryList = Replication.LogEntryProducer<IRaftLogEntry>;

    [ExcludeFromCodeCoverage]
    public sealed class InMemoryAuditTrailTests : Assert
    {
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

        [Fact]
        public static async Task Appending()
        {
            IPersistentState auditTrail = new InMemoryAuditTrail();
            Equal(0, auditTrail.GetLastIndex(false));
            Equal(0, auditTrail.GetLastIndex(true));
            var entry1 = new TestLogEntry("SET X=0") { Term = 1 };
            var entry2 = new TestLogEntry("SET Y=0") { Term = 2 };
            Equal(1, await auditTrail.AppendAsync(new LogEntryList(entry1, entry2)));
            Equal(0, auditTrail.GetLastIndex(true));
            Equal(2, auditTrail.GetLastIndex(false));
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker = (entries, snapshotIndex) =>
            {
                Equal(2, entries.Count);
                var e1 = (TestLogEntry)entries[0];
                var e2 = (TestLogEntry)entries[1];
                Equal("SET X=0", e1.Content);
                Equal("SET Y=0", e2.Content);
                return default;
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1, 2, CancellationToken.None);
            //now replace entry at index 2 with new entry
            entry2 = new TestLogEntry("ADD") { Term = 3 };
            await auditTrail.AppendAsync(entry2, 2);
            checker = (entries, snapshotIndex) =>
            {
                Equal(2, entries.Count);
                var e1 = (TestLogEntry)entries[0];
                var e2 = (TestLogEntry)entries[1];
                Equal("SET X=0", e1.Content);
                Equal("ADD", e2.Content);
                return default;
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1, 2, CancellationToken.None);
            Equal(2, auditTrail.GetLastIndex(false));
            Equal(0, auditTrail.GetLastIndex(true));
            //commit all entries
            Equal(2, await auditTrail.CommitAsync(CancellationToken.None));
            await auditTrail.WaitForCommitAsync(2, TimeSpan.Zero);
            Equal(2, auditTrail.GetLastIndex(true));
            //check overlapping with committed entries
            await ThrowsAsync<InvalidOperationException>(() => auditTrail.AppendAsync(new LogEntryList(entry1, entry2), 2).AsTask());
            await auditTrail.AppendAsync(new LogEntryList(entry1, entry2), 2, true);
            Equal(3, auditTrail.GetLastIndex(false));
            Equal(2, auditTrail.GetLastIndex(true));
        }
    }
}
