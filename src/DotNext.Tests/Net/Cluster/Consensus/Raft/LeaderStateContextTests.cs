using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Membership;

public sealed class LeaderStateContextTests : Test
{
    private sealed class DummyRaftClusterMember : IRaftClusterMember
    {
        Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => Task.FromException<Result<bool>>(new NotImplementedException());

        Task<Result<HeartbeatResult>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
            => Task.FromException<Result<HeartbeatResult>>(new NotImplementedException());

        Task<Result<HeartbeatResult>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
            => Task.FromException<Result<HeartbeatResult>>(new NotImplementedException());

        Task<long?> IRaftClusterMember.SynchronizeAsync(long commitIndex, CancellationToken token)
            => Task.FromException<long?>(new NotImplementedException());

        ValueTask IRaftClusterMember.CancelPendingRequestsAsync() => ValueTask.FromException(new NotImplementedException());

        bool IClusterMember.IsLeader => false;

        bool IClusterMember.IsRemote => false;

        ClusterMemberStatus IClusterMember.Status => ClusterMemberStatus.Unknown;

        Task<bool> IClusterMember.ResignAsync(CancellationToken token)
            => Task.FromException<bool>(new NotImplementedException());

        ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
            => ValueTask.FromException<IReadOnlyDictionary<string, string>>(new NotImplementedException());

        EndPoint IPeer.EndPoint => throw new NotImplementedException();

        event Action<ClusterMemberStatusChangedEventArgs> IClusterMember.MemberStatusChanged
        {
            add => throw new NotImplementedException();
            remove => throw new NotImplementedException();
        }

        ref IRaftClusterMember.ReplicationState IRaftClusterMember.State => throw new NotImplementedException();
    }

    [Fact]
    public static void GetOrCreate()
    {
        using var context = new LeaderState<DummyRaftClusterMember>.Context(2);
        var key1 = new DummyRaftClusterMember();
        var key2 = new DummyRaftClusterMember();

        var ctx1 = context.GetOrCreate(key1, CreateReplicator);
        var ctx2 = context.GetOrCreate(key2, CreateReplicator);

        NotSame(ctx1, ctx2);

        Same(ctx1, context.GetOrCreate(key1, CreateReplicator));
        Same(ctx2, context.GetOrCreate(key2, CreateReplicator));

        GC.KeepAlive(key1);
        GC.KeepAlive(key2);
    }

    [Fact]
    public static void Resize()
    {
        using var context = new LeaderState<DummyRaftClusterMember>.Context(2);
        var key1 = new DummyRaftClusterMember();
        var key2 = new DummyRaftClusterMember();
        var key3 = new DummyRaftClusterMember();
        var key4 = new DummyRaftClusterMember();
        var key5 = new DummyRaftClusterMember();

        var ctx1 = context.GetOrCreate(key1, CreateReplicator);
        var ctx2 = context.GetOrCreate(key2, CreateReplicator);
        var ctx3 = context.GetOrCreate(key3, CreateReplicator);
        var ctx4 = context.GetOrCreate(key4, CreateReplicator);
        var ctx5 = context.GetOrCreate(key5, CreateReplicator);

        Same(ctx1, context.GetOrCreate(key1, CreateReplicator));
        Same(ctx2, context.GetOrCreate(key2, CreateReplicator));
        Same(ctx3, context.GetOrCreate(key3, CreateReplicator));
        Same(ctx4, context.GetOrCreate(key4, CreateReplicator));
        Same(ctx5, context.GetOrCreate(key5, CreateReplicator));

        GC.KeepAlive(key1);
        GC.KeepAlive(key2);
        GC.KeepAlive(key3);
        GC.KeepAlive(key4);
        GC.KeepAlive(key5);
    }

    private static LeaderState<DummyRaftClusterMember>.ReplicationWorkItem CreateReplicator(DummyRaftClusterMember member)
        => new(member, NullLogger.Instance);
}