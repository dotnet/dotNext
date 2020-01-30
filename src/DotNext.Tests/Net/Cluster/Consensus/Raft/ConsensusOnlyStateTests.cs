using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

    [ExcludeFromCodeCoverage]
    public sealed class ConsensusOnlyStateTests : Test
    {
        [Fact]
        public static async Task RaftPersistentState()
        {
            IPersistentState auditTrail = new ConsensusOnlyState();
            NotNull(auditTrail.First);
            Equal(0, auditTrail.First.Term);
            await auditTrail.UpdateTermAsync(10);
            Equal(10, auditTrail.Term);
            await auditTrail.IncrementTermAsync();
            Equal(11, auditTrail.Term);
        }

        [Fact]
        public static async Task EmptyLogEntry()
        {
            IPersistentState auditTrail = new ConsensusOnlyState();
            await auditTrail.AppendAsync(new EmptyEntry(10));
            Equal(1, auditTrail.GetLastIndex(false));
            await auditTrail.CommitAsync(1L);
            Equal(1, auditTrail.GetLastIndex(true));
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker = (entries, snapshotIndex) =>
            {
                Equal(1, snapshotIndex);
                Equal(10, entries[0].Term);
                Equal(0, entries[0].Length);
                True(entries[0].IsReusable);
                True(entries[0].IsSnapshot);
                return new ValueTask();
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1L);
        }

        [Fact]
        public static async Task Appending()
        {
            IPersistentState auditTrail = new ConsensusOnlyState();
            Equal(0, auditTrail.GetLastIndex(false));
            Equal(0, auditTrail.GetLastIndex(true));
            var entry1 = new EmptyEntry(41);
            var entry2 = new EmptyEntry(42);
            Equal(1, await auditTrail.AppendAsync(new LogEntryList(entry1, entry2)));
            Equal(0, auditTrail.GetLastIndex(true));
            Equal(2, auditTrail.GetLastIndex(false));
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker = (entries, snapshotIndex) =>
            {
                Null(snapshotIndex);
                Equal(2, entries.Count);
                Equal(41, entries[0].Term);
                False(entries[0].IsSnapshot);
                Equal(42, entries[1].Term);
                False(entries[1].IsSnapshot);
                return default;
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1, 2, CancellationToken.None);
            //now replace entry at index 2 with new entry
            entry2 = new EmptyEntry(43);
            await auditTrail.AppendAsync(entry2, 2);
            checker = (entries, snapshotIndex) =>
            {
                Null(snapshotIndex);
                Equal(2, entries.Count);
                Equal(41, entries[0].Term);
                False(entries[0].IsSnapshot);
                Equal(43, entries[1].Term);
                False(entries[1].IsSnapshot);
                return default;
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1, 2, CancellationToken.None);
            Equal(2, auditTrail.GetLastIndex(false));
            Equal(0, auditTrail.GetLastIndex(true));
            //commit all entries
            Equal(2, await auditTrail.CommitAsync(CancellationToken.None));
            True(await auditTrail.WaitForCommitAsync(2, TimeSpan.Zero));
            Equal(2, auditTrail.GetLastIndex(true));
            //check overlapping with committed entries
            await ThrowsAsync<InvalidOperationException>(() => auditTrail.AppendAsync(new LogEntryList(entry1, entry2), 2).AsTask());
            await auditTrail.AppendAsync(new LogEntryList(entry1, entry2), 2, true);
            Equal(3, auditTrail.GetLastIndex(false));
            Equal(2, auditTrail.GetLastIndex(true));
            checker = (entries, snapshotIndex) =>
            {
                NotNull(snapshotIndex);
                Equal(2, snapshotIndex);
                Equal(2, entries.Count);
                Equal(43, entries[0].Term);
                True(entries[0].IsSnapshot);
                Equal(43, entries[1].Term);
                False(entries[1].IsSnapshot);
                return default;
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1, 3, CancellationToken.None);
        }

        [Fact]
        public static async Task DropRecords()
        {
            IPersistentState auditTrail = new ConsensusOnlyState();
            Equal(1, await auditTrail.AppendAsync(new LogEntryList(new EmptyEntry(42), new EmptyEntry(43), new EmptyEntry(44))));
            Equal(2, await auditTrail.DropAsync(2L));
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker = (entries, snapshotIndex) =>
            {
                Null(snapshotIndex);
                Equal(1, entries.Count);
                Equal(42, entries[0].Term);
                False(entries[0].IsSnapshot);
                return default;
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1, CancellationToken.None);
        }
    }
}