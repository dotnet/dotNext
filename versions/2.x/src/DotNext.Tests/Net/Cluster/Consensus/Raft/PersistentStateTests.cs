using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using LogEntryList = IO.Log.LogEntryProducer<IRaftLogEntry>;

    [ExcludeFromCodeCoverage]
    public sealed class PersistentStateTests : Test
    {
        private sealed class ClusterMemberMock : IRaftClusterMember
        {
            internal ClusterMemberMock(IPEndPoint endpoint) => Endpoint = endpoint;

            Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
                => throw new NotImplementedException();

            Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
                => throw new NotImplementedException();

            Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
                => throw new NotImplementedException();

            ref long IRaftClusterMember.NextIndex => throw new NotImplementedException();

            void IRaftClusterMember.CancelPendingRequests() => throw new NotImplementedException();

            public IPEndPoint Endpoint { get; }

            bool IClusterMember.IsLeader => false;

            bool IClusterMember.IsRemote => false;

            event ClusterMemberStatusChanged IClusterMember.MemberStatusChanged
            {
                add => throw new NotImplementedException();
                remove => throw new NotImplementedException();
            }

            ClusterMemberStatus IClusterMember.Status => ClusterMemberStatus.Unknown;

            ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
                => throw new NotImplementedException();

            Task<bool> IClusterMember.ResignAsync(CancellationToken token) => throw new NotImplementedException();

            public bool Equals(IClusterMember other) => Equals(Endpoint, other?.Endpoint);

            public override bool Equals(object other) => Equals(other as IClusterMember);

            public override int GetHashCode() => Endpoint.GetHashCode();

            public override string ToString() => Endpoint.ToString();
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

        private sealed class TestAuditTrail : PersistentState
        {
            internal long Value;

            private sealed class SimpleSnapshotBuilder : SnapshotBuilder
            {
                private long currentValue;

                protected override async ValueTask ApplyAsync(LogEntry entry)
                {
                    currentValue = await entry.ReadAsync<long>();
                }

                public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                    => writer.WriteAsync(currentValue, token);
            }

            internal TestAuditTrail(string path, bool useCaching)
                : base(path, RecordsPerPartition, new Options { UseCaching = useCaching })
            {
            }

            protected override async ValueTask ApplyAsync(LogEntry entry) => Value = await entry.ReadAsync<long>();

            protected override SnapshotBuilder CreateSnapshotBuilder() => new SimpleSnapshotBuilder();
        }

        private const int RecordsPerPartition = 4;

        [Fact]
        public static async Task StateManipulations()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            var member = new ClusterMemberMock(new IPEndPoint(IPAddress.IPv6Loopback, 3232));
            try
            {
                Equal(0, state.Term);
                Equal(1, await state.IncrementTermAsync());
                True(state.IsVotedFor(null));
                await state.UpdateVotedForAsync(member);
                False(state.IsVotedFor(null));
                True(state.IsVotedFor(member));
            }
            finally
            {
                await (state as IAsyncDisposable).DisposeAsync();
            }
            //now open state again to check persistence
            state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(1, state.Term);
                False(state.IsVotedFor(null));
                True(state.IsVotedFor(member));
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task EmptyLogEntry()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using var auditTrail = new PersistentState(dir, RecordsPerPartition);
            await auditTrail.AppendAsync(new EmptyEntry(10), CancellationToken.None);
            Equal(1, auditTrail.GetLastIndex(false));
            await auditTrail.CommitAsync(1L, CancellationToken.None);
            Equal(1, auditTrail.GetLastIndex(true));
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker = (entries, snapshotIndex) =>
            {
                Equal(10, entries[0].Term);
                Equal(0, entries[0].Length);
                False(entries[0].IsReusable);
                False(entries[0].IsSnapshot);
                return new ValueTask();
            };
            await auditTrail.ReadAsync<TestReader, DBNull>(checker, 1L, CancellationToken.None);
            Equal(0L, await auditTrail.CommitAsync(CancellationToken.None));
        }

        [Fact]
        public static async Task QueryAppendEntries()
        {
            var entry = new TestLogEntry("SET X = 0") { Term = 42L };
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker;
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                checker = (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(1L, entries.Count);
                    Equal(state.First, entries[0]);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 0L, CancellationToken.None);

                Equal(1L, await state.AppendAsync(entry));
                checker = async (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(2, entries.Count);
                    Equal(state.First, entries[0]);
                    Equal(42L, entries[1].Term);
                    Equal(entry.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 0L, CancellationToken.None);
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
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(1L, await state.AppendAsync(new LogEntryList(entry)));
                Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker2 = async (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(2, entries.Count);
                    Equal(state.First, entries[0]);
                    Equal(42L, entries[1].Term);
                    Equal(entry.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                };
                Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker1 = async (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(2, entries.Count);
                    Equal(state.First, entries[0]);
                    Equal(42L, entries[1].Term);
                    Equal(entry.Content, await entries[1].ToStringAsync(Encoding.UTF8));
                    //execute reader inside of another reader which is not possible for InMemoryAuditTrail
                    await state.ReadAsync<TestReader, DBNull>(checker2, 0L, CancellationToken.None);
                };
                await state.ReadAsync<TestReader, DBNull>(checker1, 0L, CancellationToken.None);
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task DropRecords()
        {
            var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
            var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
            var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
            var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
            var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };

            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using var state = new PersistentState(dir, RecordsPerPartition);
            Equal(1L, await state.AppendAsync(new LogEntryList(entry1, entry2, entry3, entry4, entry5)));
            Equal(5L, state.GetLastIndex(false));
            Equal(0L, state.GetLastIndex(true));
            Equal(5L, await state.DropAsync(1L, CancellationToken.None));
            Equal(0L, state.GetLastIndex(false));
            Equal(0L, state.GetLastIndex(true));
        }

        [Fact]
        public static async Task Overwrite()
        {
            var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
            var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
            var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
            var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
            var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker;
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var state = new PersistentState(dir, RecordsPerPartition))
            {
                Equal(1L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));
                Equal(4L, state.GetLastIndex(false));
                Equal(0L, state.GetLastIndex(true));
                await state.AppendAsync(entry1, 1L);
                Equal(1L, state.GetLastIndex(false));
                Equal(0L, state.GetLastIndex(true));
            }

            //read again
            using (var state = new PersistentState(dir, RecordsPerPartition))
            {
                Equal(1L, state.GetLastIndex(false));
                Equal(0L, state.GetLastIndex(true));
                checker = async (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(1, entries.Count);
                    False(entries[0].IsSnapshot);
                    Equal(entry1.Content, await entries[0].ToStringAsync(Encoding.UTF8));
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 1L, CancellationToken.None);
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
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker;
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition, new PersistentState.Options { UseCaching = useCaching, InitialPartitionSize = 1024 * 1024 });
            try
            {
                checker = (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(1L, entries.Count);
                    Equal(state.First, entries[0]);
                    False(entries[0].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 0L, CancellationToken.None);

                Equal(1L, await state.AppendAsync(new LogEntryList(entry1)));
                Equal(2L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));

                checker = async (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    Equal(6, entries.Count);
                    False(entries[0].IsSnapshot);
                    Equal(state.First, entries[0]);
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
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 0L, CancellationToken.None);
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }

            //read again
            state = new PersistentState(dir, RecordsPerPartition, new PersistentState.Options { UseCaching = useCaching, InitialPartitionSize = 1024 * 1024 });
            try
            {
                checker = async (entries, snapshotIndex) =>
                {
                    Null(snapshotIndex);
                    False(entries[0].IsSnapshot);
                    Equal(6, entries.Count);
                    Equal(state.First, entries[0]);
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
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 0L, CancellationToken.None);
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

            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var state = new PersistentState(dir, RecordsPerPartition, new PersistentState.Options { UseCaching = useCaching }))
            {
                Equal(1L, await state.AppendAsync(new LogEntryList(entry1)));
                Equal(2L, await state.AppendAsync(new LogEntryList(entry2, entry3, entry4, entry5)));

                Equal(1L, await state.CommitAsync(1L, CancellationToken.None));
                Equal(2L, await state.CommitAsync(3L, CancellationToken.None));
                Equal(0L, await state.CommitAsync(2L, CancellationToken.None));
                Equal(3L, state.GetLastIndex(true));
                Equal(5L, state.GetLastIndex(false));

                await ThrowsAsync<InvalidOperationException>(() => state.AppendAsync(entry1, 1L).AsTask());
            }

            //read again
            using (var state = new PersistentState(dir, RecordsPerPartition, new PersistentState.Options { UseCaching = useCaching }))
            {
                Equal(3L, state.GetLastIndex(true));
                Equal(5L, state.GetLastIndex(false));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task SnapshotInstallation(bool useCaching)
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, long index) => entry = new Int64LogEntry(42L + index) { Term = index });
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker;
            using (var state = new TestAuditTrail(dir, useCaching))
            {
                await state.AppendAsync(new LogEntryList(entries));
                Equal(3, await state.CommitAsync(3, CancellationToken.None));
                //install snapshot and erase all existing entries up to 7th (inclusive)
                await state.AppendAsync(new Int64LogEntry(100500L, true), 7);
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(3, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 6, 9, CancellationToken.None).ConfigureAwait(false);
            }

            //read again
            using (var state = new TestAuditTrail(dir, useCaching))
            {
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(3, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 6, 9, CancellationToken.None).ConfigureAwait(false);
                await state.AppendAsync(new Int64LogEntry(90L, true), 11);
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(1, readResult.Count);
                    Equal(11, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 6, 9, CancellationToken.None).ConfigureAwait(false);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task Compaction(bool useCaching)
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, long index) => entry = new Int64LogEntry(42L + index) { Term = index });
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker;
            using (var state = new TestAuditTrail(dir, useCaching))
            {
                await state.AppendAsync(new LogEntryList(entries));
                await state.CommitAsync(CancellationToken.None);
                Equal(entries.Length + 41L, state.Value);
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(1, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 1, 6, CancellationToken.None);
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(3, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 1, CancellationToken.None);
            }

            //read agian
            using (var state = new TestAuditTrail(dir, useCaching))
            {
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(1, readResult.Count);
                    NotNull(snapshotIndex);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 1, 6, CancellationToken.None);
                Equal(0L, state.Value);
                checker = (readResult, snapshotIndex) =>
                {
                    Equal(3, readResult.Count);
                    Equal(7, snapshotIndex);
                    True(readResult[0].IsSnapshot);
                    False(readResult[1].IsSnapshot);
                    False(readResult[2].IsSnapshot);
                    return default;
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 1, CancellationToken.None);
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
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var backupFile = Path.GetTempFileName();
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            var member = new ClusterMemberMock(new IPEndPoint(IPAddress.IPv6Loopback, 3232));
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
            state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(5, state.GetLastIndex(false));
                Equal(2, state.GetLastIndex(true));
                Func<IReadOnlyList<IRaftLogEntry>, long?, ValueTask> checker = (entries, snapshotIndex) =>
                {
                    Equal(entry1.Term, entries[0].Term);
                    Equal(entry2.Term, entries[1].Term);
                    Equal(entry3.Term, entries[2].Term);
                    Equal(entry4.Term, entries[3].Term);
                    Equal(entry5.Term, entries[4].Term);
                    return new ValueTask();
                };
                await state.ReadAsync<TestReader, DBNull>(checker, 1L, 5L);
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
            entries.ForEach((ref Int64LogEntry entry, long index) => entry = new Int64LogEntry(42L + index) { Term = index });
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var state = new TestAuditTrail(dir, true))
            {
                await state.AppendAsync(new LogEntryList(entries));
                await state.CommitAsync(CancellationToken.None);
                Equal(entries.Length + 41L, state.Value);
            }

            //reconstruct state
            using (var state = new TestAuditTrail(dir, true))
            {
                Equal(0L, state.Value);
                await state.InitializeAsync();
                Equal(entries.Length + 41L, state.Value);
            }
        }
    }
}