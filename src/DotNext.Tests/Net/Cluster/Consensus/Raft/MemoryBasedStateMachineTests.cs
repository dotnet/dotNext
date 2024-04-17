﻿using System.Net;
using System.Reflection;
using System.Text;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers.Binary;
using IO;
using Text.Json;
using IRaftLog = IO.Log.IAuditTrail<IRaftLogEntry>;
using LogEntryConsumer = IO.Log.LogEntryConsumer<IRaftLogEntry, Missing>;
using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

public sealed class MemoryBasedStateMachineTests : Test
{
    private sealed class Int64LogEntry : BlittableTransferObject<long>, IRaftLogEntry
    {
        required public long Term { get; init; }

        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        public bool IsSnapshot { get; init; }
    }

    private sealed class PersistentStateWithoutSnapshot : MemoryBasedStateMachine
    {
        internal PersistentStateWithoutSnapshot(string path, int recordsPerPartition, Options configuration = null)
            : base(path, recordsPerPartition, OrDefault(configuration))
        {
        }

        private static Options OrDefault(Options configuration)
        {
            configuration ??= new();
            configuration.CompactionMode = CompactionMode.Background;
            return configuration;
        }

        protected override ValueTask ApplyAsync(LogEntry entry)
        {
            False(entry.IsEmpty);
            True(entry.GetReader().TryGetRemainingBytesCount(out var length));
            NotEqual(0L, length);
            return ValueTask.CompletedTask;
        }

        protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
            => throw new NotImplementedException();
    }

    private sealed class PersistentStateWithSnapshot : MemoryBasedStateMachine
    {
        private Blittable<long> value;

        internal long Value
        {
            get => value.Value;
            set => this.value.Value = value;
        }

        private sealed class SimpleSnapshotBuilder(in SnapshotBuilderContext context) : IncrementalSnapshotBuilder(in context)
        {
            private Blittable<long> snapshot;

            protected internal override async ValueTask ApplyAsync(LogEntry entry)
            {
                True(entry.Index > 0L);
                snapshot = await entry.GetReader().ReadAsync<Blittable<long>>();
            }

            public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(snapshot, token);
        }

        internal PersistentStateWithSnapshot(string path, bool useCaching, CompactionMode compactionMode = default)
            : base(path, RecordsPerPartition, new Options { UseCaching = useCaching, CompactionMode = compactionMode, IntegrityCheck = true, WriteMode = WriteMode.AutoFlush })
        {
        }

        internal new Task ClearAsync(CancellationToken token = default) => base.ClearAsync(token);

        protected override async ValueTask ApplyAsync(LogEntry entry) => value = await entry.GetReader().ReadAsync<Blittable<long>>();

        protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context) => new SimpleSnapshotBuilder(context);
    }

    private const int RecordsPerPartition = 4;

    [Fact]
    public static async Task StateManipulations()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
        var member = ClusterMemberId.FromEndPoint(new IPEndPoint(IPAddress.IPv6Loopback, 3232));
        try
        {
            Equal(0, state.Term);
            Equal(1, await state.IncrementTermAsync(default));
            True(state.IsVotedFor(default(ClusterMemberId)));
            await state.UpdateVotedForAsync(member);
            False(state.IsVotedFor(default(ClusterMemberId)));
            True(state.IsVotedFor(member));
        }
        finally
        {
            (state as IDisposable).Dispose();
        }

        //now open state again to check persistence
        state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
        try
        {
            Equal(1, state.Term);
            False(state.IsVotedFor(default(ClusterMemberId)));
            True(state.IsVotedFor(member));
        }
        finally
        {
            (state as IDisposable).Dispose();
        }
    }

    [Fact]
    public static async Task EmptyLogEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var auditTrail = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
        await auditTrail.AppendAsync(new EmptyLogEntry { Term = 10 });

        Equal(1, auditTrail.LastEntryIndex);
        await auditTrail.CommitAsync(1L, CancellationToken.None);
        Equal(1, auditTrail.LastCommittedEntryIndex);
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (entries, snapshotIndex, token) =>
        {
            Equal(10, entries[0].Term);
            Equal(0, entries[0].Length);
            True(entries[0].IsReusable);
            False(entries[0].IsSnapshot);
            return default;
        };
        await auditTrail.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1L, CancellationToken.None);
        Equal(0L, await auditTrail.CommitAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(0L, true, 65)]
    [InlineData(1024, true, 65)]
    [InlineData(0L, false, 65)]
    [InlineData(1024, false, 65)]
    public static async Task QueryAppendEntries(long partitionSize, bool caching, int concurrentReads)
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { MaxConcurrentReads = concurrentReads, InitialPartitionSize = partitionSize, UseCaching = caching });
        try
        {
            // entry 1
            checker = (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Equal(1L, entries.Count);
                Equal(0L, entries[0].Term);
                return default;
            };
            await state.ReadAsync(new LogEntryConsumer(checker) { LogEntryMetadataOnly = true }, 0L, CancellationToken.None);

            Equal(1L, await state.AppendAsync(entry1));
            checker = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);

                Equal(2, entries.Count);
                Equal(0L, entries.First().Term);          // element 0
                Equal(42L, entries.Skip(1).First().Term); // element 1
                Equal(entry1.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                return Missing.Value;
            };

            await state.ReadAsync(new LogEntryConsumer(checker), 0L, CancellationToken.None);

            // entry 2
            Equal(2L, await state.AppendAsync(entry2));
            checker = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Single(entries);
                Equal(43L, entries[0].Term);
                Equal(entry2.Content, await entries[0].ToStringAsync(Encoding.UTF8));
                return Missing.Value;
            };

            await state.ReadAsync(new LogEntryConsumer(checker), 2L, CancellationToken.None);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1024L * 1024L * 100L)]
    public static async Task ParallelReads(long? maxLogEntrySize)
    {
        var entry = new TestLogEntry("SET X = 0") { Term = 42L };
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new() { CopyOnReadOptions = new(), MaxLogEntrySize = maxLogEntrySize });
        try
        {
            Equal(1L, await state.AppendAsync(new LogEntryList(entry)));
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker2 = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Equal(2, entries.Count);
                Equal(0L, entries[0].Term);
                Equal(42L, entries[1].Term);
                Equal(entry.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                return Missing.Value;
            };
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker1 = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Equal(2, entries.Count);
                Equal(0L, entries[0].Term);
                Equal(42L, entries[1].Term);
                Equal(entry.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                //execute reader inside of another reader which is not possible for ConsensusOnlyState
                return await state.ReadAsync(new LogEntryConsumer(checker2), 0L, CancellationToken.None);
            };
            await state.ReadAsync(new LogEntryConsumer(checker1), 0L, CancellationToken.None);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1024L * 1024L * 100L)]
    public static async Task AppendWhileReading(long? maxLogEntrySize)
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new() { MaxLogEntrySize = maxLogEntrySize });
        var entry = new TestLogEntry("SET X = 0") { Term = 42L };
        await state.AppendAsync(entry);

        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<long>> checker = async (entries, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            Equal(2, entries.Count);
            Equal(0L, entries[0].Term);
            Equal(42L, entries[1].Term);

            Equal(entry.Content, await entries[1].ToStringAsync(Encoding.UTF8));

            // append a new log entry
            return await state.AppendAsync(new TestLogEntry("SET Y = 42") { Term = 43L });
        };

        var index = await state.ReadAsync(new IO.Log.LogEntryConsumer<IRaftLogEntry, long>(checker), 0L);
        Equal(2L, index);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task DropRecords(bool reuseSpace)
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
        Equal(1L, await state.AppendAsync(new LogEntryList(entry1, entry2, entry3, entry4, entry5)));
        Equal(5L, state.LastEntryIndex);
        Equal(0L, state.LastCommittedEntryIndex);
        Equal(5L, await state.DropAsync(1L, reuseSpace));
        Equal(0L, state.LastEntryIndex);
        Equal(0L, state.LastCommittedEntryIndex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1024L * 1024L * 100L)]
    public static async Task Overwrite(long? maxLogEntrySize)
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new() { MaxLogEntrySize = maxLogEntrySize }))
        {
            Equal(1L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));
            Equal(4L, state.LastEntryIndex);
            Equal(0L, state.LastCommittedEntryIndex);
            await state.AppendAsync(entry1, 1L);
            Equal(1L, state.LastEntryIndex);
            Equal(0L, state.LastCommittedEntryIndex);
        }

        //read again
        using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new() { MaxLogEntrySize = maxLogEntrySize }))
        {
            Equal(1L, state.LastEntryIndex);
            Equal(0L, state.LastCommittedEntryIndex);
            checker = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Single(entries);
                False(entries[0].IsSnapshot);
                Equal(entry1.Content, await entries[0].ToStringAsync(Encoding.UTF8));
                return Missing.Value;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1L, CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task PartitionOverflow(bool useCaching)
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = useCaching, InitialPartitionSize = 1024 * 1024 });
        try
        {
            checker = (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Equal(1L, entries.Count);
                Equal(0L, entries[0].Term);
                False(entries[0].IsSnapshot);
                return default;
            };
            await state.ReadAsync(new LogEntryConsumer(checker) { LogEntryMetadataOnly = true }, 0L, CancellationToken.None);

            Equal(1L, await state.AppendAsync(new LogEntryList(entry1)));
            Equal(2L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));

            checker = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                Equal(6, entries.Count);
                False(entries[0].IsSnapshot);
                Equal(0L, entries[0].Term);
                Equal(42L, entries[1].Term);
                Equal(entry1.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                Equal(entry1.Timestamp, entries[1].Timestamp);
                Equal(43L, entries[2].Term);
                Equal(entry2.Content, await entries[2].ToStringAsync(Encoding.UTF8));
                Equal(entry2.Timestamp, entries[2].Timestamp);
                Equal(44L, entries[3].Term);
                Equal(entry3.Content, await entries[3].ToStringAsync(Encoding.UTF8));
                Equal(entry3.Timestamp, entries[3].Timestamp);
                Equal(45L, entries[4].Term);
                Equal(entry4.Content, await entries[4].ToStringAsync(Encoding.UTF8));
                Equal(entry4.Timestamp, entries[4].Timestamp);
                Equal(46L, entries[5].Term);
                Equal(entry5.Content, await entries[5].ToStringAsync(Encoding.UTF8));
                Equal(entry5.Timestamp, entries[5].Timestamp);
                return default;
            };
            await state.ReadAsync(new LogEntryConsumer(checker), 0L, CancellationToken.None);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }

        //read again
        state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = useCaching, InitialPartitionSize = 1024 * 1024 });
        try
        {
            checker = async (entries, snapshotIndex, token) =>
            {
                Null(snapshotIndex);
                False(entries[0].IsSnapshot);
                Equal(6, entries.Count);
                Equal(0L, entries[0].Term);
                Equal(42L, entries[1].Term);
                Equal(entry1.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                Equal(entry1.Timestamp, entries[1].Timestamp);
                Equal(43L, entries[2].Term);
                Equal(entry2.Content, await entries[2].ToStringAsync(Encoding.UTF8));
                Equal(entry2.Timestamp, entries[2].Timestamp);
                Equal(44L, entries[3].Term);
                Equal(entry3.Content, await entries[3].ToStringAsync(Encoding.UTF8));
                Equal(entry3.Timestamp, entries[3].Timestamp);
                Equal(45L, entries[4].Term);
                Equal(entry4.Content, await entries[4].ToStringAsync(Encoding.UTF8));
                Equal(entry4.Timestamp, entries[4].Timestamp);
                Equal(46L, entries[5].Term);
                Equal(entry5.Content, await entries[5].ToStringAsync(Encoding.UTF8));
                Equal(entry5.Timestamp, entries[5].Timestamp);
                return Missing.Value;
            };
            await state.ReadAsync(new LogEntryConsumer(checker), 0L, CancellationToken.None);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task Commit(bool useCaching)
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = useCaching }))
        {
            Equal(1L, await state.AppendAsync(entry1, true));
            Equal(2L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));

            Equal(1L, await state.CommitAsync(1L, CancellationToken.None));
            Equal(2L, await state.CommitAsync(3L, CancellationToken.None));
            Equal(0L, await state.CommitAsync(2L, CancellationToken.None));
            Equal(3L, state.LastCommittedEntryIndex);
            Equal(5L, state.LastEntryIndex);

            await ThrowsAsync<InvalidOperationException>(() => state.AppendAsync(entry1, 1L).AsTask());
        }

        //read again
        using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = useCaching }))
        {
            Equal(3L, state.LastCommittedEntryIndex);
            Equal(5L, state.LastEntryIndex);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task SnapshotInstallation(bool useCaching)
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Term = index, Content = 42L + index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        using (var state = new PersistentStateWithSnapshot(dir, useCaching))
        {
            await state.AppendAsync(new LogEntryList(entries));
            Equal(3, await state.CommitAsync(3, CancellationToken.None));
            //install snapshot and erase all existing entries up to 7th (inclusive)
            await state.AppendAsync(new Int64LogEntry { Content = 100500L, IsSnapshot = true, Term = 0L }, 7);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(3, readResult.Count);
                Equal(7, snapshotIndex);
                True(readResult[0].IsSnapshot);
                False(readResult[1].IsSnapshot);
                False(readResult[2].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 6, 9, CancellationToken.None);
        }

        //read again
        using (var state = new PersistentStateWithSnapshot(dir, useCaching))
        {
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(3, readResult.Count);
                Equal(7, snapshotIndex);
                True(readResult[0].IsSnapshot);
                False(readResult[1].IsSnapshot);
                False(readResult[2].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker) { LogEntryMetadataOnly = true }, 6, 9, CancellationToken.None);
            await state.AppendAsync(new Int64LogEntry { Content = 90L, IsSnapshot = true, Term = 0L }, 11);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Single(readResult);
                Equal(11, snapshotIndex);
                True(readResult[0].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 6, 9, CancellationToken.None);
        }
    }

    [Fact]
    public static async Task RewriteLogEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = true });
        Equal(1L, await state.AppendAsync(new TestLogEntry("SET X = 0") { Term = 42L }, true));
        await state.AppendAsync(new EmptyLogEntry { Term = 43L }, 1L);

        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (readResult, snapshotIndex, token) =>
        {
            Null(snapshotIndex);
            NotEmpty(readResult);
            return default;
        };
        await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1L, 1L);
    }

    [Fact]
    public static async Task ClearLog()
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        using var state = new PersistentStateWithSnapshot(dir, useCaching: false);
        await state.AppendAsync(new LogEntryList(entries));
        Equal(3, await state.CommitAsync(3, CancellationToken.None));
        //install snapshot and erase all existing entries up to 7th (inclusive)
        await state.AppendAsync(new Int64LogEntry { Content = 100500L, IsSnapshot = true, Term = 0L }, 7);
        checker = static (readResult, snapshotIndex, token) =>
        {
            Equal(3, readResult.Count);
            Equal(7, snapshotIndex);
            True(readResult[0].IsSnapshot);
            False(readResult[1].IsSnapshot);
            False(readResult[2].IsSnapshot);
            return default;
        };

        await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 6, 9, CancellationToken.None);
        await state.ClearAsync();

        Equal(0L, state.LastCommittedEntryIndex);
        Equal(0L, state.LastEntryIndex);
    }

    [Theory]
    [InlineData(MemoryBasedStateMachine.CompactionMode.Background)]
    [InlineData(MemoryBasedStateMachine.CompactionMode.Foreground)]
    [InlineData(MemoryBasedStateMachine.CompactionMode.Sequential)]
    [InlineData(MemoryBasedStateMachine.CompactionMode.Incremental)]
    public static async Task AppendAndCommitAsync(MemoryBasedStateMachine.CompactionMode compaction)
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var state = new PersistentStateWithSnapshot(dir, true, compaction);
        Equal(0L, await state.As<IRaftLog>().AppendAndCommitAsync(new LogEntryList(entries), 1L, false, 0L));
        Equal(0L, state.LastCommittedEntryIndex);
        Equal(9L, state.LastEntryIndex);

        Equal(9L, await state.As<IRaftLog>().AppendAndCommitAsync(new LogEntryList(entries), 10L, false, 9L));
        Equal(9L, state.LastCommittedEntryIndex);
        Equal(18L, state.LastEntryIndex);

        Equal(9L, await state.As<IRaftLog>().AppendAndCommitAsync(new LogEntryList(entries), 19L, false, 18L));
        Equal(18L, state.LastCommittedEntryIndex);
        Equal(27L, state.LastEntryIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task SequentialCompaction(bool useCaching)
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        using (var state = new PersistentStateWithSnapshot(dir, useCaching, MemoryBasedStateMachine.CompactionMode.Sequential))
        {
            False(state.IsBackgroundCompaction);
            await state.AppendAsync(new LogEntryList(entries));
            Equal(0L, state.CompactionCount);
            await state.CommitAsync(CancellationToken.None);
            Equal(entries.Length + 41L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Single(readResult);
                Equal(9, snapshotIndex);
                True(readResult[0].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
        }

        //read again
        using (var state = new PersistentStateWithSnapshot(dir, useCaching))
        {
            checker = static (readResult, snapshotIndex, token) =>
            {
                Single(readResult);
                NotNull(snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
            Equal(0L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Single(readResult);
                Equal(9, snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task BackgroundCompaction(bool useCaching)
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        using (var state = new PersistentStateWithSnapshot(dir, useCaching, MemoryBasedStateMachine.CompactionMode.Background))
        {
            True(state.IsBackgroundCompaction);
            await state.AppendAsync(new LogEntryList(entries));
            Equal(0L, state.CompactionCount);
            await state.CommitAsync(CancellationToken.None);
            Equal(1L, state.CompactionCount);
            Equal(entries.Length + 41L, state.Value);
            await state.ForceCompactionAsync(1L, CancellationToken.None);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(3, readResult.Count);
                Equal(4, snapshotIndex);
                True(readResult[0].IsSnapshot);
                False(readResult[1].IsSnapshot);
                False(readResult[2].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(6, readResult.Count);
                Equal(4, snapshotIndex);
                True(readResult[0].IsSnapshot);
                False(readResult[1].IsSnapshot);
                False(readResult[2].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
        }

        //read again
        using (var state = new PersistentStateWithSnapshot(dir, useCaching))
        {
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(3, readResult.Count);
                NotNull(snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
            Equal(0L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(6, readResult.Count);
                Equal(4, snapshotIndex);
                True(readResult[0].IsSnapshot);
                False(readResult[1].IsSnapshot);
                False(readResult[2].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task ForegroundCompaction(bool useCaching)
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        using (var state = new PersistentStateWithSnapshot(dir, useCaching, MemoryBasedStateMachine.CompactionMode.Foreground))
        {
            False(state.IsBackgroundCompaction);
            await state.AppendAsync(new LogEntryList(entries));
            Equal(0L, state.CompactionCount);
            await state.CommitAsync(3, CancellationToken.None);
            await state.CommitAsync(CancellationToken.None);
            Equal(entries.Length + 41L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(4, readResult.Count);
                Equal(3, snapshotIndex);
                True(readResult[0].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
        }

        //read again
        using (var state = new PersistentStateWithSnapshot(dir, useCaching))
        {
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(4, readResult.Count);
                NotNull(snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
            Equal(0L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(7, readResult.Count);
                Equal(3, snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task IncrementalCompaction(bool useCaching)
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
        using (var state = new PersistentStateWithSnapshot(dir, useCaching, MemoryBasedStateMachine.CompactionMode.Incremental))
        {
            False(state.IsBackgroundCompaction);
            await state.AppendAsync(new LogEntryList(entries));
            await state.CommitAsync(4, CancellationToken.None);
            await state.CommitAsync(CancellationToken.None);
            Equal(entries.Length + 41L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(3, readResult.Count);
                Equal(4, snapshotIndex);
                True(readResult[0].IsSnapshot);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
        }

        //read again
        using (var state = new PersistentStateWithSnapshot(dir, useCaching))
        {
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(3, readResult.Count);
                NotNull(snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, 6, CancellationToken.None);
            Equal(0L, state.Value);
            checker = static (readResult, snapshotIndex, token) =>
            {
                Equal(6, readResult.Count);
                Equal(4, snapshotIndex);
                return default;
            };
            await state.As<IRaftLog>().ReadAsync(new LogEntryConsumer(checker), 1, CancellationToken.None);
        }
    }

    [Fact]
    public static async Task RestoreBackup()
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var backupFile = Path.GetTempFileName();
        IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
        var member = ClusterMemberId.FromEndPoint(new IPEndPoint(IPAddress.IPv6Loopback, 3232));
        try
        {
            //define node state
            Equal(1, await state.IncrementTermAsync(member));
            True(state.IsVotedFor(member));
            //define log entries
            Equal(1L, await state.AppendAsync(new LogEntryList(entry1, entry2, entry3, entry4, entry5)));
            //commit some of them
            Equal(2L, await state.CommitAsync(2L));
            //save backup
            await using var backupStream = new FileStream(backupFile, FileMode.Truncate, FileAccess.Write, FileShare.None, 1024, true);
            await state.CreateBackupAsync(backupStream);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }

        //restore state from backup
        await using (var backupStream = new FileStream(backupFile, FileMode.Open, FileAccess.Read, FileShare.None, 1024, true))
        {
            await PersistentState.RestoreFromBackupAsync(backupStream, new DirectoryInfo(dir));
        }

        //ensure that all entries are recovered successfully
        state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
        try
        {
            Equal(5, state.LastEntryIndex);
            Equal(2, state.LastCommittedEntryIndex);
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = (entries, snapshotIndex, token) =>
            {
                Equal(entry1.Term, entries[0].Term);
                Equal(entry2.Term, entries[1].Term);
                Equal(entry3.Term, entries[2].Term);
                Equal(entry4.Term, entries[3].Term);
                Equal(entry5.Term, entries[4].Term);
                return default;
            };
            await state.ReadAsync(new LogEntryConsumer(checker), 1L, 5L);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public static async Task CreateSparseBackup()
    {
        var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
        var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
        var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
        var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
        var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var backupFile = Path.GetTempFileName();
        IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new() { MaxLogEntrySize = 1024 * 1024, BackupFormat = System.Formats.Tar.TarEntryFormat.Gnu });
        var member = ClusterMemberId.FromEndPoint(new IPEndPoint(IPAddress.IPv6Loopback, 3232));
        try
        {
            //define node state
            Equal(1, await state.IncrementTermAsync(member));
            True(state.IsVotedFor(member));
            //define log entries
            Equal(1L, await state.AppendAsync(new LogEntryList(entry1, entry2, entry3, entry4, entry5)));
            //commit some of them
            Equal(2L, await state.CommitAsync(2L));
            //save backup
            await using var backupStream = new FileStream(backupFile, FileMode.Truncate, FileAccess.Write, FileShare.None, 1024, true);
            await state.CreateBackupAsync(backupStream);
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public static async Task Reconstruction()
    {
        var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
        entries.AsSpan().ForEach((ref Int64LogEntry entry, int index) => entry = new Int64LogEntry { Content = 42L + index, Term = index });
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using (var state = new PersistentStateWithSnapshot(dir, true))
        {
            await state.AppendAsync(new LogEntryList(entries));
            await state.CommitAsync(CancellationToken.None);
            Equal(entries.Length + 41L, state.Value);
        }

        //reconstruct state
        using (var state = new PersistentStateWithSnapshot(dir, true))
        {
            Equal(0L, state.Value);
            await state.InitializeAsync();
            Equal(entries.Length + 41L, state.Value);
        }
    }

    [Fact]
    public static void ReadWriteConcurrently()
    {
        using var manager = new PersistentState.LockManager(10);
        True(manager.AcquireAsync(PersistentState.LockType.WeakReadLock).IsCompletedSuccessfully);
        True(manager.AcquireAsync(PersistentState.LockType.WriteLock).IsCompletedSuccessfully);
        True(manager.AcquireAsync(PersistentState.LockType.WeakReadLock).IsCompletedSuccessfully);
        False(manager.TryAcquire(PersistentState.LockType.WriteLock));
        False(manager.TryAcquire(PersistentState.LockType.ExclusiveLock));
    }

    [Fact]
    public static void CombineCompactionAndWriteLock()
    {
        using var manager = new PersistentState.LockManager(10);
        True(manager.AcquireAsync(PersistentState.LockType.WriteLock).IsCompletedSuccessfully);
        True(manager.AcquireAsync(PersistentState.LockType.CompactionLock).IsCompletedSuccessfully);
        False(manager.TryAcquire(PersistentState.LockType.WriteLock));
        False(manager.TryAcquire(PersistentState.LockType.WeakReadLock));
        False(manager.TryAcquire(PersistentState.LockType.StrongReadLock));

        manager.Release(PersistentState.LockType.ExclusiveLock);
        True(manager.AcquireAsync(PersistentState.LockType.ExclusiveLock).IsCompletedSuccessfully);
    }

    [Fact]
    public static void StrongWeakLock()
    {
        using var manager = new PersistentState.LockManager(10);
        True(manager.AcquireAsync(PersistentState.LockType.StrongReadLock).IsCompletedSuccessfully);
        True(manager.AcquireAsync(PersistentState.LockType.WeakReadLock).IsCompletedSuccessfully);
        False(manager.TryAcquire(PersistentState.LockType.WriteLock));

        manager.Release(PersistentState.LockType.StrongReadLock);
        True(manager.AcquireAsync(PersistentState.LockType.WriteLock).IsCompletedSuccessfully);
    }

    private sealed class JsonPersistentState : MemoryBasedStateMachine
    {
        private readonly List<TestJsonObject> entries = new();

        internal JsonPersistentState(string location, bool caching)
            : base(location, RecordsPerPartition, new Options { UseCaching = caching, CompactionMode = CompactionMode.Background })
        {
        }

        protected override async ValueTask ApplyAsync(LogEntry entry)
        {
            var content = await JsonSerializable<TestJsonObject>.TransformAsync(entry);
            entries.Add(content);
        }

        protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
            => throw new NotImplementedException();

        internal IReadOnlyList<TestJsonObject> Entries => entries;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task JsonSerialization(bool cached)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using var state = new JsonPersistentState(dir, cached);
        var entry1 = state.CreateJsonLogEntry(new TestJsonObject { StringField = "Entry1" });
        var entry2 = state.CreateJsonLogEntry(new TestJsonObject { StringField = "Entry2" });
        await state.AppendAsync(entry1, true);
        await state.AppendAsync(entry2, true);
        await state.CommitAsync(CancellationToken.None);
        Equal(2, state.Entries.Count);

        var payload = state.Entries[0];
        Equal(entry1.Content.StringField.Value, payload.StringField.Value);

        payload = state.Entries[1];
        Equal(entry2.Content.StringField.Value, payload.StringField.Value);
    }
}