using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Net;
using System.Reflection;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using IRaftLog = IO.Log.IAuditTrail<IRaftLogEntry>;
    using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

    [ExcludeFromCodeCoverage]
    public sealed class MemoryBasedStateMachineTests : Test
    {
        private sealed class Int64LogEntry : BinaryTransferObject, IRaftLogEntry
        {
            internal Int64LogEntry(long value, bool snapshot = false)
                : base(ToMemory(value))
            {
                Timestamp = DateTimeOffset.UtcNow;
                IsSnapshot = snapshot;
            }

            public long Term { get; set; }

            public DateTimeOffset Timestamp { get; }

            public bool IsSnapshot { get; }

            private static ReadOnlyMemory<byte> ToMemory(long value)
            {
                var result = new Memory<byte>(new byte[sizeof(long)]);
                WriteInt64LittleEndian(result.Span, value);
                return result;
            }
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
                return ValueTask.CompletedTask;
            }

            protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
                => throw new NotImplementedException();
        }

        private sealed class PersistentStateWithSnapshot : MemoryBasedStateMachine
        {
            internal long Value;

            private sealed class SimpleSnapshotBuilder : IncrementalSnapshotBuilder
            {
                private long currentValue;

                public SimpleSnapshotBuilder(in SnapshotBuilderContext context)
                    : base(in context)
                {
                }

                protected internal override async ValueTask ApplyAsync(LogEntry entry)
                {
                    Assert.True(entry.Index > 0L);
                    currentValue = await entry.ToTypeAsync<long, LogEntry>();
                }

                public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                    => writer.WriteAsync(currentValue, token);
            }

            internal PersistentStateWithSnapshot(string path, bool useCaching, MemoryBasedStateMachine.CompactionMode compactionMode = default)
                : base(path, RecordsPerPartition, new Options { UseCaching = useCaching, CompactionMode = compactionMode, IntegrityCheck = true, WriteMode = WriteMode.AutoFlush })
            {
            }

            internal new Task ClearAsync(CancellationToken token = default) => base.ClearAsync(token);

            protected override async ValueTask ApplyAsync(LogEntry entry) => Value = await entry.ToTypeAsync<long, LogEntry>();

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
                Equal(1, await state.IncrementTermAsync());
                True(state.IsVotedFor(default(ClusterMemberId?)));
                await state.UpdateVotedForAsync(member);
                False(state.IsVotedFor(default(ClusterMemberId?)));
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
                False(state.IsVotedFor(default(ClusterMemberId?)));
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
            await auditTrail.AppendAsync(new EmptyLogEntry(10));

            Equal(1, auditTrail.LastUncommittedEntryIndex);
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
            await auditTrail.As<IRaftLog>().ReadAsync(checker, 1L, CancellationToken.None);
            Equal(0L, await auditTrail.CommitAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData(0L, true, 65)]
        [InlineData(1024, true, 65)]
        [InlineData(0L, false, 65)]
        [InlineData(1024, false, 65)]
        [InlineData(0L, true, 3)]
        [InlineData(1024, true, 3)]
        [InlineData(0L, false, 3)]
        [InlineData(1024, false, 3)]
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
                await state.ReadAsync(checker, 0L, CancellationToken.None);

                Equal(1L, await state.AppendAsync(entry1));
                checker = async (entries, snapshotIndex, token) =>
                {
                    Null(snapshotIndex);
                    Equal(2, entries.Count);
                    Equal(0L, entries[0].Term);
                    Equal(42L, entries[1].Term);
                    Equal(entry1.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                    return Missing.Value;
                };

                await state.ReadAsync(checker, 0L, CancellationToken.None);

                // entry 2
                Equal(2L, await state.AppendAsync(entry2));
                checker = async (entries, snapshotIndex, token) =>
                {
                    Null(snapshotIndex);
                    Equal(1, entries.Count);
                    Equal(43L, entries[0].Term);
                    Equal(entry2.Content, await entries[0].ToStringAsync(Encoding.UTF8));
                    return Missing.Value;
                };

                await state.ReadAsync(checker, 2L, CancellationToken.None);
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task ParallelReads()
        {
            var entry = new TestLogEntry("SET X = 0") { Term = 42L };
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            IPersistentState state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new() { CopyOnReadOptions = new() });
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
                    return await state.ReadAsync(checker2, 0L, CancellationToken.None);
                };
                await state.ReadAsync(checker1, 0L, CancellationToken.None);
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task AppendWhileReading()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition);
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

            var index = await state.As<IPersistentState>().ReadAsync(checker, 0L);
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
            Equal(5L, state.LastUncommittedEntryIndex);
            Equal(0L, state.LastCommittedEntryIndex);
            Equal(5L, await state.DropAsync(1L, reuseSpace));
            Equal(0L, state.LastUncommittedEntryIndex);
            Equal(0L, state.LastCommittedEntryIndex);
        }

        [Fact]
        public static async Task Overwrite()
        {
            var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
            var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
            var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
            var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
            var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition))
            {
                Equal(1L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));
                Equal(4L, state.LastUncommittedEntryIndex);
                Equal(0L, state.LastCommittedEntryIndex);
                await state.AppendAsync(entry1, 1L);
                Equal(1L, state.LastUncommittedEntryIndex);
                Equal(0L, state.LastCommittedEntryIndex);
            }

            //read again
            using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition))
            {
                Equal(1L, state.LastUncommittedEntryIndex);
                Equal(0L, state.LastCommittedEntryIndex);
                checker = async (entries, snapshotIndex, token) =>
                {
                    Null(snapshotIndex);
                    Equal(1, entries.Count);
                    False(entries[0].IsSnapshot);
                    Equal(entry1.Content, await entries[0].ToStringAsync(Encoding.UTF8));
                    return Missing.Value;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1L, CancellationToken.None);
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
                await state.ReadAsync(checker, 0L, CancellationToken.None);

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
                await state.ReadAsync(checker, 0L, CancellationToken.None);
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
                await state.ReadAsync(checker, 0L, CancellationToken.None);
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
                Equal(5L, state.LastUncommittedEntryIndex);

                await ThrowsAsync<InvalidOperationException>(() => state.AppendAsync(entry1, 1L).AsTask());
            }

            //read again
            using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = useCaching }))
            {
                Equal(3L, state.LastCommittedEntryIndex);
                Equal(5L, state.LastUncommittedEntryIndex);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task SnapshotInstallation(bool useCaching)
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, nint index) => entry = new Int64LogEntry(42L + index) { Term = index });
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
            using (var state = new PersistentStateWithSnapshot(dir, useCaching))
            {
                await state.AppendAsync(new LogEntryList(entries));
                Equal(3, await state.CommitAsync(3, CancellationToken.None));
                //install snapshot and erase all existing entries up to 7th (inclusive)
                await state.AppendAsync(new Int64LogEntry(100500L, true), 7);
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(3, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 6, 9, CancellationToken.None);
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
                await state.As<IRaftLog>().ReadAsync(checker, 6, 9, CancellationToken.None);
                await state.AppendAsync(new Int64LogEntry(90L, true), 11);
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(1, readResult.Count);
                    Equal(11, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 6, 9, CancellationToken.None);
            }
        }

        [Fact]
        public static async Task RewriteLogEntry()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using (var state = new PersistentStateWithoutSnapshot(dir, RecordsPerPartition, new MemoryBasedStateMachine.Options { UseCaching = true }))
            {
                Equal(1L, await state.AppendAsync(new TestLogEntry("SET X = 0") { Term = 42L }, true));
                await state.AppendAsync(new EmptyLogEntry(43L), 1L);

                Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker = static (readResult, snapshotIndex, token) =>
                {
                    Null(snapshotIndex);
                    NotEmpty(readResult);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1L, 1L);
            }
        }

        [Fact]
        public static async Task ClearPersistentLog()
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, nint index) => entry = new Int64LogEntry(42L + index) { Term = index });
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
            using (var state = new PersistentStateWithSnapshot(dir, useCaching: false))
            {
                await state.AppendAsync(new LogEntryList(entries));
                Equal(3, await state.CommitAsync(3, CancellationToken.None));
                //install snapshot and erase all existing entries up to 7th (inclusive)
                await state.AppendAsync(new Int64LogEntry(100500L, true), 7);
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(3, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };

                await state.As<IRaftLog>().ReadAsync(checker, 6, 9, CancellationToken.None);
                await state.ClearAsync();

                Equal(0L, state.LastCommittedEntryIndex);
                Equal(0L, state.LastUncommittedEntryIndex);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task SequentialCompaction(bool useCaching)
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, nint index) => entry = new Int64LogEntry(42L + index) { Term = index });
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
                    Equal(1, readResult.Count);
                    Equal(9, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, 6, CancellationToken.None);
                await state.As<IRaftLog>().ReadAsync(checker, 1, CancellationToken.None);
            }

            //read agian
            using (var state = new PersistentStateWithSnapshot(dir, useCaching))
            {
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(1, readResult.Count);
                    NotNull(snapshotIndex);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, 6, CancellationToken.None);
                Equal(0L, state.Value);
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(1, readResult.Count);
                    Equal(9, snapshotIndex);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, CancellationToken.None);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task BackgroundCompaction(bool useCaching)
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, nint index) => entry = new Int64LogEntry(42L + index) { Term = index });
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
                await state.As<IRaftLog>().ReadAsync(checker, 1, 6, CancellationToken.None);
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(6, readResult.Count);
                    Equal(4, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, CancellationToken.None);
            }

            //read agian
            using (var state = new PersistentStateWithSnapshot(dir, useCaching))
            {
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(3, readResult.Count);
                    NotNull(snapshotIndex);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, 6, CancellationToken.None);
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
                await state.As<IRaftLog>().ReadAsync(checker, 1, CancellationToken.None);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task ForegroundCompaction(bool useCaching)
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, nint index) => entry = new Int64LogEntry(42L + index) { Term = index });
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
                await state.As<IRaftLog>().ReadAsync(checker, 1, 6, CancellationToken.None);
            }

            //read agian
            using (var state = new PersistentStateWithSnapshot(dir, useCaching))
            {
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(4, readResult.Count);
                    NotNull(snapshotIndex);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, 6, CancellationToken.None);
                Equal(0L, state.Value);
                checker = static (readResult, snapshotIndex, token) =>
                {
                    Equal(7, readResult.Count);
                    Equal(3, snapshotIndex);
                    return default;
                };
                await state.As<IRaftLog>().ReadAsync(checker, 1, CancellationToken.None);
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
                Equal(1, await state.IncrementTermAsync());
                await state.UpdateVotedForAsync(member);
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
                Equal(5, state.LastUncommittedEntryIndex);
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
                await state.ReadAsync(checker, 1L, 5L);
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
            entries.ForEach((ref Int64LogEntry entry, nint index) => entry = new Int64LogEntry(42L + index) { Term = index });
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

        private sealed class AsyncLockSettings : PersistentState.IAsyncLockSettings
        {
            public int ConcurrencyLevel => 10;

            IncrementingEventCounter PersistentState.IAsyncLockSettings.LockContentionCounter => null;

            EventCounter PersistentState.IAsyncLockSettings.LockDurationCounter => null;
        }

        [Fact]
        public static void ReadWriteConcurrently()
        {
            using var manager = new PersistentState.LockManager(new AsyncLockSettings());
            True(manager.AcquireAsync(PersistentState.LockType.WeakReadLock).IsCompletedSuccessfully);
            True(manager.AcquireAsync(PersistentState.LockType.WriteLock).IsCompletedSuccessfully);
            True(manager.AcquireAsync(PersistentState.LockType.WeakReadLock).IsCompletedSuccessfully);
            False(manager.AcquireAsync(PersistentState.LockType.WriteLock, TimeSpan.Zero).Result);
            False(manager.AcquireAsync(PersistentState.LockType.ExclusiveLock, TimeSpan.Zero).Result);
        }

        [Fact]
        public static void CombineCompactionAndWriteLock()
        {
            using var manager = new PersistentState.LockManager(new AsyncLockSettings());
            True(manager.AcquireAsync(PersistentState.LockType.WriteLock).IsCompletedSuccessfully);
            True(manager.AcquireAsync(PersistentState.LockType.CompactionLock).IsCompletedSuccessfully);
            False(manager.AcquireAsync(PersistentState.LockType.WriteLock, TimeSpan.Zero).Result);
            False(manager.AcquireAsync(PersistentState.LockType.WeakReadLock, TimeSpan.Zero).Result);
            False(manager.AcquireAsync(PersistentState.LockType.StrongReadLock, TimeSpan.Zero).Result);

            manager.Release(PersistentState.LockType.ExclusiveLock);
            True(manager.AcquireAsync(PersistentState.LockType.ExclusiveLock).IsCompletedSuccessfully);
        }

        [Fact]
        public static void StrongWeakLock()
        {
            using var manager = new PersistentState.LockManager(new AsyncLockSettings());
            True(manager.AcquireAsync(PersistentState.LockType.StrongReadLock).IsCompletedSuccessfully);
            True(manager.AcquireAsync(PersistentState.LockType.WeakReadLock).IsCompletedSuccessfully);
            False(manager.AcquireAsync(PersistentState.LockType.WriteLock, TimeSpan.Zero).Result);

            manager.Release(PersistentState.LockType.StrongReadLock);
            True(manager.AcquireAsync(PersistentState.LockType.WriteLock).IsCompletedSuccessfully);
        }

        public struct JsonPayload
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Message { get; set; }
        }

        private sealed class JsonPersistentState : MemoryBasedStateMachine
        {
            private readonly List<object> entries = new();

            internal JsonPersistentState(string location, bool caching)
                : base(location, RecordsPerPartition, new Options { UseCaching = caching, CompactionMode = CompactionMode.Background })
            {
            }

            protected override async ValueTask ApplyAsync(LogEntry entry)
            {
                var content = await entry.DeserializeFromJsonAsync();
                entries.Add(content);
            }

            protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
                => throw new NotImplementedException();

            internal IReadOnlyList<object> Entries => entries;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task JsonSerialization(bool cached)
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using var state = new JsonPersistentState(dir, cached);
            var entry1 = state.CreateJsonLogEntry<JsonPayload>(new JsonPayload { X = 10, Y = 20, Message = "Entry1" });
            var entry2 = state.CreateJsonLogEntry<JsonPayload>(new JsonPayload { X = 50, Y = 60, Message = "Entry2" });
            await state.AppendAsync(entry1, true);
            await state.AppendAsync(entry2, true);
            await state.CommitAsync(CancellationToken.None);
            Equal(2, state.Entries.Count);

            var payload = (JsonPayload)state.Entries[0];
            Equal(entry1.Content.X, payload.X);
            Equal(entry1.Content.Y, payload.Y);
            Equal(entry1.Content.Message, payload.Message);

            payload = (JsonPayload)state.Entries[1];
            Equal(entry2.Content.X, payload.X);
            Equal(entry2.Content.Y, payload.Y);
            Equal(entry2.Content.Message, payload.Message);
        }
    }
}