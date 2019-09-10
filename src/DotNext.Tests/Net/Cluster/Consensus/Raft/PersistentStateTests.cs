using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using static Messaging.Messenger;

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
        }
    }
}