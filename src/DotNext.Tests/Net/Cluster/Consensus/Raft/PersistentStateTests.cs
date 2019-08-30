using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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

            Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync(long term, IReadOnlyList<ILogEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
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
        private const long MaxRecordSize = 1024;

        [Fact]
        public static async Task StateManipulations()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IPersistentState state = new PersistentState(dir, RecordsPerPartition, MaxRecordSize);
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
            state = new PersistentState(dir, RecordsPerPartition, MaxRecordSize);
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
            IPersistentState state = new PersistentState(dir, RecordsPerPartition, MaxRecordSize);
            try
            {
                var entries = await state.GetEntriesAsync(0L);
                Equal(1L, entries.Count);
                Equal(state.First, entries[0]);
                entries = await state.GetEntriesAsync(1L);
                Equal(0L, entries.Count);

                Equal(1L, await state.AppendAsync(new[] { entry }));
                entries = await state.GetEntriesAsync(0L);
                Equal(2, entries.Count);
                Equal(state.First, entries[0]);
                Equal(42L, entries[1].Term);
                Equal("SET X = 0", await entries[1].ReadAsTextAsync());
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }
    }
}