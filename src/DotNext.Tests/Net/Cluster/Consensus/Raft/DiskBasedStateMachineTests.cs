using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using static System.Buffers.Binary.BinaryPrimitives;
using Missing = System.Reflection.Missing;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;
    using IRaftLog = IO.Log.IAuditTrail<IRaftLogEntry>;
    using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

    [ExcludeFromCodeCoverage]
    public sealed class DiskBasedStateMachineTests : Test
    {
        private sealed class SimpleStateMachine : DiskBasedStateMachine
        {
            private const int RecordsPerPartition = 4;
            private const int InMemoryCapacity = 3;

            private byte[] snapshot;
            private readonly List<long> values;

            internal SimpleStateMachine(string path, Options configuration = null)
                : base(path, RecordsPerPartition, configuration)
            {
                snapshot = new byte[sizeof(long)];
                values = new(InMemoryCapacity);
            }

            internal long Value => values.Count == 0 ? BitConverter.ToInt64(snapshot) : values.Last();

            protected override async ValueTask<long?> ApplyAsync(LogEntry entry)
            {
                var value = await entry.ToTypeAsync<long, LogEntry>();
                values.Add(value);

                long? result;
                if (values.Count > InMemoryCapacity)
                {
                    snapshot = BitConverter.GetBytes(value);
                    values.Clear();
                    result = sizeof(long);
                }
                else
                {
                    result = null;
                }

                return result;
            }

            protected override ValueTask<IAsyncBinaryReader> BeginReadSnapshotAsync(SnapshotAccessToken session, MemoryAllocator<byte> allocator, CancellationToken token)
                => new(IAsyncBinaryReader.Create(new ReadOnlyMemory<byte>(snapshot)));

            protected override void EndReadSnapshot(SnapshotAccessToken session)
            {
            }

            protected override async ValueTask<long> InstallSnapshotAsync<TSnapshot>(TSnapshot snapshot)
            {
                this.snapshot = await snapshot.ToByteArrayAsync();
                values.Clear();
                return sizeof(long);
            }
        }

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

        [Fact]
        public static async Task EmptyLogEntry()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var auditTrail = new SimpleStateMachine(dir);
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
            var entry1 = new Int64LogEntry(100500) { Term = 42L };
            var entry2 = new Int64LogEntry(100501) { Term = 43L };
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
            IPersistentState state = new SimpleStateMachine(dir, new DiskBasedStateMachine.Options { MaxConcurrentReads = concurrentReads, InitialPartitionSize = partitionSize, UseCaching = caching });
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
                    Equal(entry1.Content.ToArray(), await entries[1].ToByteArrayAsync());
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
                    Equal(entry2.Content.ToArray(), await entries[0].ToByteArrayAsync());
                    return Missing.Value;
                };

                await state.ReadAsync(checker, 2L, CancellationToken.None);
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task PartitionOverflow(bool useCaching)
        {
            var entry1 = new Int64LogEntry(100500) { Term = 42L };
            var entry2 = new Int64LogEntry(100501) { Term = 43L };
            var entry3 = new Int64LogEntry(100502) { Term = 44L };
            var entry4 = new Int64LogEntry(100503) { Term = 45L };
            var entry5 = new Int64LogEntry(100504) { Term = 46L };
            Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<Missing>> checker;
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            IPersistentState state = new SimpleStateMachine(dir, new DiskBasedStateMachine.Options { UseCaching = useCaching, InitialPartitionSize = 1024 * 1024 });
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
                    Equal(entry1.Content.ToArray(), await entries[1].ToByteArrayAsync());
                    Equal(entry1.Timestamp, entries[1].Timestamp);
                    Equal(43L, entries[2].Term);
                    Equal(entry2.Content.ToArray(), await entries[2].ToByteArrayAsync());
                    Equal(entry2.Timestamp, entries[2].Timestamp);
                    Equal(44L, entries[3].Term);
                    Equal(entry3.Content.ToArray(), await entries[3].ToByteArrayAsync());
                    Equal(entry3.Timestamp, entries[3].Timestamp);
                    Equal(45L, entries[4].Term);
                    Equal(entry4.Content.ToArray(), await entries[4].ToByteArrayAsync());
                    Equal(entry4.Timestamp, entries[4].Timestamp);
                    Equal(46L, entries[5].Term);
                    Equal(entry5.Content.ToArray(), await entries[5].ToByteArrayAsync());
                    Equal(entry5.Timestamp, entries[5].Timestamp);
                    return default;
                };

                await state.ReadAsync(checker, 0L, CancellationToken.None);

                await state.CommitAsync();

                checker = async (entries, snapshotIndex, token) =>
                {
                    NotNull(snapshotIndex);
                    Equal(2, entries.Count);

                    True(entries[0].IsSnapshot);
                    Equal(entry4.Term, entries[0].Term);
                    Equal(entry4.Content.ToArray(), await entries[0].ToByteArrayAsync());

                    Equal(entry5.Term, entries[1].Term);
                    Equal(entry5.Content.ToArray(), await entries[1].ToByteArrayAsync());
                    return default;
                };

                await state.ReadAsync(checker, 0L, CancellationToken.None);
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }
    }
}