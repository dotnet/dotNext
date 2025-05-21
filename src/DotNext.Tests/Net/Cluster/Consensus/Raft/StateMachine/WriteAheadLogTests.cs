using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers.Binary;
using static IO.DataTransferObject;
using LogEntryConsumer = IO.Log.LogEntryConsumer<IRaftLogEntry, Missing>;
using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

[Experimental("DOTNEXT001")]
public sealed class WriteAheadLogTests : Test
{
    [Fact]
    public static async Task LockManager()
    {
        await using var lockManager = new WriteAheadLog.LockManager(3);
        await lockManager.AcquireReadLockAsync();

        var readBarrierTask = lockManager.AcquireReadBarrierAsync().AsTask();
        lockManager.ReleaseReadLock();

        await readBarrierTask.WaitAsync(DefaultTimeout);
    }
    
    [Fact]
    public static async Task StateManipulations()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        IPersistentState state;
        var member = ClusterMemberId.FromEndPoint(new IPEndPoint(IPAddress.IPv6Loopback, 3232));

        await using (var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine()))
        {
            state = wal;
            Equal(0, state.Term);
            Equal(1, await state.IncrementTermAsync(default));
            True(state.IsVotedFor(default));
            await state.UpdateVotedForAsync(member);
            False(state.IsVotedFor(default));
            True(state.IsVotedFor(member));
        }

        //now open state again to check persistence
        await using (var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine()))
        {
            state = wal;
            Equal(1, state.Term);
            False(state.IsVotedFor(default));
            True(state.IsVotedFor(member));
        }
    }
    
    [Fact]
    public static async Task EmptyLogEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var auditTrail = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine());
        await auditTrail.AppendAsync(new EmptyLogEntry { Term = 10 });

        Equal(1, auditTrail.LastEntryIndex);
        Equal(1L, await auditTrail.CommitAsync(1L));
        Equal(1, auditTrail.LastCommittedEntryIndex);
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (entries, snapshotIndex, _) =>
        {
            Equal(10, entries[0].Term);
            Equal(0, entries[0].Length);
            True(entries[0].IsReusable);
            False(entries[0].IsSnapshot);
            return default;
        };
        await auditTrail.ReadAsync(new LogEntryConsumer(checker), 1L, auditTrail.LastEntryIndex);
        Equal(0L, await auditTrail.CommitAsync(auditTrail.LastEntryIndex));
    }

    [Fact]
    public static async Task ContextFlow()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var stateMachine = new NoOpStateMachine();
        await using var wal = new WriteAheadLog(new() { Location = dir }, stateMachine);

        const string context = "Context";
        Equal(1L, await wal.AppendAsync(new Blittable<long> { Value = 42L }, context));
        Equal(2L, await wal.AppendAsync(ReadOnlyMemory<byte>.Empty, context: null));

        await wal.CommitAsync(wal.LastEntryIndex);
        await wal.WaitForApplyAsync(2L);

        Equal(context, Contains(1L, stateMachine.Context));
    }

    [Fact]
    public static async Task QueryAppendEntries()
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine());

        // entry 1
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = (entries, snapshotIndex, _) =>
        {
            Null(snapshotIndex);
            Equal(1L, entries.Count);
            Equal(0L, entries[0].Term);
            return default;
        };
        await wal.ReadAsync(new LogEntryConsumer(checker) { LogEntryMetadataOnly = true }, 0L, wal.LastEntryIndex);

        Equal(1L, await wal.AppendAsync(entry1));
        checker = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);

            Equal(2, entries.Count);
            Equal(0L, entries.First().Term); // element 0
            Equal(42L, entries.Skip(1).First().Term); // element 1
            Equal(entry1.Content, await entries[1].ToStringAsync(Encoding.UTF8, token: token));
            return Missing.Value;
        };

        await wal.ReadAsync(new LogEntryConsumer(checker), 0L, wal.LastEntryIndex);

        // entry 2
        Equal(2L, await wal.AppendAsync(entry2));
        checker = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Single(entries);
            Equal(43L, entries[0].Term);
            Equal(entry2.Content, await entries[0].ToStringAsync(Encoding.UTF8, token: token));
            return Missing.Value;
        };

        await wal.ReadAsync(new LogEntryConsumer(checker), 2L, wal.LastEntryIndex);
    }

    [Fact]
    public static async Task ParallelReads()
    {
        ReadOnlyMemory<byte> payload = RandomBytes(64);
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine());

        Equal(1L, await wal.AppendAsync(payload));
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker2 = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Equal(2, entries.Count);
            Equal(0L, entries[0].Term);
            Equal(wal.As<IPersistentState>().Term, entries[1].Term);
            Equal(payload, await entries[1].ToByteArrayAsync(token: token));
            return Missing.Value;
        };
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker1 = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Equal(2, entries.Count);
            Equal(0L, entries[0].Term);
            Equal(payload, await entries[1].ToByteArrayAsync(token: token));
            
            //execute reader inside of another reader which is not possible for ConsensusOnlyState
            return await wal.ReadAsync(new LogEntryConsumer(checker2), 0L, wal.LastEntryIndex, token);
        };
        await wal.ReadAsync(new LogEntryConsumer(checker1), 0L, wal.LastEntryIndex);
    }
    
    [Fact]
    public static async Task AppendWhileReading()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine());

        ReadOnlyMemory<byte> payload = RandomBytes(64);
        await wal.AppendAsync(payload);

        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<long>> checker = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Equal(2, entries.Count);
            Equal(0L, entries[0].Term);

            Equal(payload, await entries[1].ToByteArrayAsync(token: token));

            // append a new log entry
            return await wal.AppendAsync(RandomBytes(64), token: token);
        };

        var index = await wal.ReadAsync(new IO.Log.LogEntryConsumer<IRaftLogEntry, long>(checker), 0L, wal.LastEntryIndex);
        Equal(2L, index);
    }

    [Fact]
    public static async Task AppendLargeEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var options = new WriteAheadLog.Options { Location = dir };
        await using var wal = new WriteAheadLog(options, new NoOpStateMachine());

        var payload = new TestLogEntry(Random.Shared.NextString(Alphabet, options.ChunkMaxSize * 2));
        Equal(1L, await wal.AppendAsync(payload));

        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            
            Equal(payload.Content, await entries[0].ToStringAsync(Encoding.UTF8, token: token));
            return Missing.Value;
        };

        await wal.ReadAsync(new LogEntryConsumer(checker), 1L, 1L);
    }

    [Fact]
    public static async Task Overwrite()
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine());
        
        await wal.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5), 1L);
        Equal(4L, wal.LastEntryIndex);
        Equal(0L, wal.LastCommittedEntryIndex);

        await wal.AppendAsync(entry1, 1L);
        Equal(1L, wal.LastEntryIndex);
        Equal(0L, wal.LastCommittedEntryIndex);
            
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Single(entries);
            False(entries[0].IsSnapshot);
            Equal(entry1.Content, await entries[0].ToStringAsync(Encoding.UTF8, token: token));
            return Missing.Value;
        };
        
        await wal.ReadAsync(new LogEntryConsumer(checker), 1L, wal.LastEntryIndex);
    }
    
    [Fact]
    public static async Task Commit()
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L, Context = 56 };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using (var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine()))
        {
            Equal(1L, await wal.AppendAsync(entry1));
            await wal.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5), 2L);

            Equal(1L, await wal.CommitAsync(1L));
            Equal(2L, await wal.CommitAsync(3L));
            Equal(0L, await wal.CommitAsync(2L));
            Equal(3L, wal.LastCommittedEntryIndex);
            Equal(5L, wal.LastEntryIndex);

            await ThrowsAsync<InvalidOperationException>(wal.AppendAsync(entry1, 1L).AsTask);
        }

        //read again
        await using (var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine()))
        {
            Equal(3L, wal.LastCommittedEntryIndex);
            Equal(3L, wal.LastEntryIndex);

            await using var enumerator = wal.ReadAsync(1L, wal.LastEntryIndex).GetAsyncEnumerator();
            
            True(await enumerator.MoveNextAsync());
            Equal(entry1.Content, await enumerator.Current.ToStringAsync(Encoding.UTF8));
            
            True(await enumerator.MoveNextAsync());
            Equal(entry2.Content, await enumerator.Current.ToStringAsync(Encoding.UTF8));
            
            True(await enumerator.MoveNextAsync());
            Equal(entry3.Content, await enumerator.Current.ToStringAsync(Encoding.UTF8));

            False(await enumerator.MoveNextAsync());
        }
    }

    [Fact]
    public static async Task IncrementalState()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var stateMachine = new SumStateMachine(new(dir));
        await using var wal = new WriteAheadLog(new() { Location = dir }, stateMachine);

        Memory<byte> buffer = new byte[sizeof(long)];
        const long count = 1000L;
        using var cts = new CancellationTokenSource();
        for (var i = 0L; i < count; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Span, i);
            
            cts.CancelAfter(DefaultTimeout);
            var index = await wal.AppendAsync(buffer, token: cts.Token);
            await wal.CommitAsync(index, cts.Token);
            await wal.WaitForApplyAsync(index, cts.Token);

            True(cts.TryReset());
        }

        Equal(count * (0L + count - 1L) / 2L, stateMachine.Value);
    }

    [Fact]
    public static async Task StateRecovery()
    {
        const long count = 1000L;
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using (var wal = new WriteAheadLog(new() { Location = dir }, new NoOpStateMachine()))
        {
            Memory<byte> buffer = new byte[sizeof(long)];
            var index = 0L;
            for (var i = 0L; i < count; i++)
            {
                BinaryPrimitives.WriteInt64LittleEndian(buffer.Span, i);
                index = await wal.AppendAsync(buffer);
            }

            await wal.CommitAsync(index);
            await wal.WaitForApplyAsync(index);
        }
        
        await using var stateMachine = new SumStateMachine(new(dir));
        await using (var wal = new WriteAheadLog(new() { Location = dir }, stateMachine))
        {
            await stateMachine.RestoreAsync();
            await wal.InitializeAsync();

            Equal(count * (0L + count - 1L) / 2L, stateMachine.Value);
        }
    }
}