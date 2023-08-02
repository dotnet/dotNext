using System.Reflection;

namespace DotNext.Net.Cluster.Consensus.Raft;

using LogEntryConsumer = IO.Log.LogEntryConsumer<IRaftLogEntry, Missing>;
using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

public sealed class ConsensusOnlyStateTests : Test
{
    [Fact]
    public static async Task RaftPersistentState()
    {
        IPersistentState auditTrail = new ConsensusOnlyState();
        await auditTrail.UpdateTermAsync(10, false);
        Equal(10, auditTrail.Term);
        await auditTrail.IncrementTermAsync(default);
        Equal(11, auditTrail.Term);
    }

    [Fact]
    public static async Task EmptyLogEntry()
    {
        IPersistentState auditTrail = new ConsensusOnlyState();
        await auditTrail.AppendAsync(new EmptyLogEntry(10));
        Equal(1, auditTrail.LastUncommittedEntryIndex);
        await auditTrail.CommitAsync(1L);
        Equal(1, auditTrail.LastCommittedEntryIndex);
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (entries, snapshotIndex, token) =>
        {
            Equal(1, snapshotIndex);
            Equal(10, entries[0].Term);
            Equal(0, entries[0].Length);
            True(entries[0].IsReusable);
            True(entries[0].IsSnapshot);
            return default;
        };
        await auditTrail.ReadAsync(new LogEntryConsumer(checker), 1L);
    }

    [Fact]
    public static async Task Appending()
    {
        IPersistentState auditTrail = new ConsensusOnlyState();
        Equal(0, auditTrail.LastUncommittedEntryIndex);
        Equal(0, auditTrail.LastCommittedEntryIndex);
        var entry1 = new EmptyLogEntry(41);
        var entry2 = new EmptyLogEntry(42);
        Equal(1, await auditTrail.AppendAsync(new LogEntryList(entry1, entry2)));
        Equal(0, auditTrail.LastCommittedEntryIndex);
        Equal(2, auditTrail.LastUncommittedEntryIndex);
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Equal(2, entries.Count);
            Equal(41, entries[0].Term);
            False(entries[0].IsSnapshot);
            Equal(42, entries[1].Term);
            False(entries[1].IsSnapshot);
            return default;
        };
        await auditTrail.ReadAsync(new LogEntryConsumer(checker), 1, 2, CancellationToken.None);
        //now replace entry at index 2 with new entry
        entry2 = new EmptyLogEntry(43);
        await auditTrail.AppendAsync(entry2, 2);
        checker = static (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Equal(2, entries.Count);
            Equal(41, entries[0].Term);
            False(entries[0].IsSnapshot);
            Equal(43, entries[1].Term);
            False(entries[1].IsSnapshot);
            return default;
        };
        await auditTrail.ReadAsync(new LogEntryConsumer(checker), 1, 2, CancellationToken.None);
        Equal(2, auditTrail.LastUncommittedEntryIndex);
        Equal(0, auditTrail.LastCommittedEntryIndex);
        //commit all entries
        Equal(2, await auditTrail.CommitAsync(CancellationToken.None));
        await auditTrail.WaitForCommitAsync(2);
        Equal(2, auditTrail.LastCommittedEntryIndex);
        //check overlapping with committed entries
        await ThrowsAsync<InvalidOperationException>(() => auditTrail.AppendAsync(new LogEntryList(entry1, entry2), 2).AsTask());
        await auditTrail.AppendAsync(new LogEntryList(entry1, entry2), 2, true);
        Equal(3, auditTrail.LastUncommittedEntryIndex);
        Equal(2, auditTrail.LastCommittedEntryIndex);
        checker = static (entries, snapshotIndex, token) =>
        {
            NotNull(snapshotIndex);
            Equal(2, snapshotIndex);
            Equal(2, entries.Count);
            Equal(2, entries.Count());
            Equal(43, entries[0].Term);
            True(entries[0].IsSnapshot);
            Equal(43, entries[1].Term);
            False(entries[1].IsSnapshot);
            return default;
        };
        await auditTrail.ReadAsync(new LogEntryConsumer(checker), 1, 3, CancellationToken.None);
    }

    [Fact]
    public static async Task DropRecords()
    {
        IPersistentState auditTrail = new ConsensusOnlyState();
        Equal(1, await auditTrail.AppendAsync(new LogEntryList(new EmptyLogEntry(42), new EmptyLogEntry(43), new EmptyLogEntry(44))));
        Equal(2, await auditTrail.DropAsync(2L));
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Single(entries);
            Equal(42, entries[0].Term);
            False(entries[0].IsSnapshot);
            return default;
        };
        await auditTrail.ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
    }
}