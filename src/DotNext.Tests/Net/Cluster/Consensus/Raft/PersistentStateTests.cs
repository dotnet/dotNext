using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using System.IO.Pipelines;
    using static Messaging.Messenger;
    using ILogEntry = Replication.ILogEntry;

    public sealed class PersistentStateTests : Assert
    {
        private sealed class ClusterMemberMock : IRaftClusterMember
        {
            internal ClusterMemberMock(IPEndPoint endpoint) => Endpoint = endpoint;

            Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
                => throw new NotImplementedException();

            Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync(long term, IReadOnlyList<IRaftLogEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
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

            ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadata(bool refresh, CancellationToken token)
                => throw new NotImplementedException();

            Task<bool> IClusterMember.ResignAsync(CancellationToken token) => throw new NotImplementedException();

            public bool Equals(IClusterMember other) => Equals(Endpoint, other?.Endpoint);

            public override bool Equals(object other) => Equals(other as IClusterMember);

            public override int GetHashCode() => Endpoint.GetHashCode();

            public override string ToString() => Endpoint.ToString();
        }

        private sealed class Int64LogEntry: BinaryTransferObject, IRaftLogEntry
        {
            internal Int64LogEntry(long value)
                : base(ToMemory(value))
            {
                Timestamp = DateTimeOffset.UtcNow;
            }

            public long Term { get; set; }

            bool ILogEntry.IsSnapshot => false;

            public DateTimeOffset Timestamp { get; }

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
                private readonly byte[] sharedBuffer;

                internal SimpleSnapshotBuilder(byte[] buffer) => sharedBuffer = buffer;

                public override Task CopyToAsync(Stream output, CancellationToken token)
                {
                    WriteInt64LittleEndian(sharedBuffer, currentValue);
                    return output.WriteAsync(sharedBuffer, 0, sizeof(long), token);
                }

                public override async ValueTask CopyToAsync(PipeWriter output, CancellationToken token)
                {
                    WriteInt64LittleEndian(sharedBuffer, currentValue);
                    await output.WriteAsync(new ReadOnlyMemory<byte>(sharedBuffer, 0, sizeof(long)), token);
                }

                protected override async ValueTask ApplyAsync(LogEntry entry)
                {
                    currentValue = ReadInt64LittleEndian((await entry.ReadAsync(sizeof(long))).Span);
                }
            }

            internal TestAuditTrail(string path)
                : base(path, RecordsPerPartition)
            {
            }

            private static async Task<long> Decode(LogEntry entry) => ReadInt64LittleEndian((await entry.ReadAsync(sizeof(long))).Span);

            protected override async ValueTask ApplyAsync(LogEntry entry) => Value = await Decode(entry);

            protected override SnapshotBuilder CreateSnapshotBuilder(byte[] buffer) => new SimpleSnapshotBuilder(buffer);
        }

        private const long RecordsPerPartition = 4;

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
                (state as IDisposable)?.Dispose();
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
        public static async Task QueryAppendEntries()
        {
            var entry = new TestLogEntry("SET X = 0") { Term = 42L };
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                var entries = await state.GetEntriesAsync(0L, CancellationToken.None);
                Equal(1L, entries.Count);
                Equal(state.First, entries[0]);
                entries.Dispose();

                Equal(1L, await state.AppendAsync(new[] { entry }));
                entries = await state.GetEntriesAsync(0L, CancellationToken.None);
                Equal(2, entries.Count);
                Equal(state.First, entries[0]);
                Equal(42L, entries[1].Term);
                Equal(entry.Content, await entries[1].ReadAsTextAsync(Encoding.UTF8));
                entries.Dispose();
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task Overwrite()
        {
            var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
            var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
            var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
            var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
            var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };

            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(1L, await state.AppendAsync(new[] { entry2, entry3, entry4, entry5 }));
                Equal(4L, state.GetLastIndex(false));
                Equal(0L, state.GetLastIndex(true));
                await state.AppendAsync(new[] { entry1 }, 1L);
                Equal(1L, state.GetLastIndex(false));
                Equal(0L, state.GetLastIndex(true));
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }

            //read again
            state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(1L, state.GetLastIndex(false));
                Equal(0L, state.GetLastIndex(true));
                var entries = await state.GetEntriesAsync(1L, CancellationToken.None);
                Equal(1, entries.Count);
                Equal(entry1.Content, await entries[0].ReadAsTextAsync(Encoding.UTF8));
                entries.Dispose();
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task PartitionOverflow()
        {
            var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
            var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
            var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
            var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
            var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
            
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                var entries = await state.GetEntriesAsync(0L, CancellationToken.None);
                Equal(1L, entries.Count);
                Equal(state.First, entries[0]);
                entries.Dispose();

                Equal(1L, await state.AppendAsync(new[] { entry1 }));
                Equal(2L, await state.AppendAsync(new[] { entry2, entry3, entry4, entry5 }));

                entries = await state.GetEntriesAsync(0L, CancellationToken.None);
                Equal(6, entries.Count);
                Equal(state.First, entries[0]);
                Equal(42L, entries[1].Term);
                Equal(entry1.Content, await entries[1].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry1.Timestamp, entries[1].Timestamp);
                Equal(43L, entries[2].Term);
                Equal(entry2.Content, await entries[2].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry2.Timestamp, entries[2].Timestamp);
                Equal(44L, entries[3].Term);
                Equal(entry3.Content, await entries[3].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry3.Timestamp, entries[3].Timestamp);
                Equal(45L, entries[4].Term);
                Equal(entry4.Content, await entries[4].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry4.Timestamp, entries[4].Timestamp);
                Equal(46L, entries[5].Term);
                Equal(entry5.Content, await entries[5].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry5.Timestamp, entries[5].Timestamp);
                entries.Dispose();
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }

            //read again
            state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                var entries = await state.GetEntriesAsync(0L, CancellationToken.None);
                Equal(6, entries.Count);
                Equal(state.First, entries[0]);
                Equal(42L, entries[1].Term);
                Equal(entry1.Content, await entries[1].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry1.Timestamp, entries[1].Timestamp);
                Equal(43L, entries[2].Term);
                Equal(entry2.Content, await entries[2].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry2.Timestamp, entries[2].Timestamp);
                Equal(44L, entries[3].Term);
                Equal(entry3.Content, await entries[3].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry3.Timestamp, entries[3].Timestamp);
                Equal(45L, entries[4].Term);
                Equal(entry4.Content, await entries[4].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry4.Timestamp, entries[4].Timestamp);
                Equal(46L, entries[5].Term);
                Equal(entry5.Content, await entries[5].ReadAsTextAsync(Encoding.UTF8));
                Equal(entry5.Timestamp, entries[5].Timestamp);
                entries.Dispose();
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task Commit()
        {
            var entry1 = new TestLogEntry("SET X = 0") { Term = 42L };
            var entry2 = new TestLogEntry("SET Y = 1") { Term = 43L };
            var entry3 = new TestLogEntry("SET Z = 2") { Term = 44L };
            var entry4 = new TestLogEntry("SET U = 3") { Term = 45L };
            var entry5 = new TestLogEntry("SET V = 4") { Term = 46L };
            
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(1L, await state.AppendAsync(new[] { entry1 }));
                Equal(2L, await state.AppendAsync(new[] { entry2, entry3, entry4, entry5 }));

                Equal(1L, await state.CommitAsync(1L, CancellationToken.None));
                Equal(2L, await state.CommitAsync(3L, CancellationToken.None));
                Equal(0L, await state.CommitAsync(2L, CancellationToken.None));
                Equal(3L, state.GetLastIndex(true));
                Equal(5L, state.GetLastIndex(false));

                await ThrowsAsync<InvalidOperationException>(() => state.AppendAsync(new[] { entry1 }, 1L));
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }

            //read again
            state = new PersistentState(dir, RecordsPerPartition);
            try
            {
                Equal(3L, state.GetLastIndex(true));
                Equal(5L, state.GetLastIndex(false));
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public static async Task Compaction()
        {
            var entries = new Int64LogEntry[RecordsPerPartition * 2 + 1];
            entries.ForEach((ref Int64LogEntry entry, long index) => entry = new Int64LogEntry(42L + index) { Term = index });
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new TestAuditTrail(dir);
            try
            {
                await state.AppendAsync(entries);
                await state.CommitAsync(CancellationToken.None);
                var readResult = await state.GetEntriesAsync(1, 6, CancellationToken.None);
                Equal(1, readResult.Count);
                True(readResult[0].IsSnapshot);
                readResult.Dispose();
                readResult = await state.GetEntriesAsync(1, CancellationToken.None);
                Equal(3, readResult.Count);
                True(readResult[0].IsSnapshot);
                False(readResult[1].IsSnapshot);
                False(readResult[2].IsSnapshot);
                readResult.Dispose();
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }
    }
}