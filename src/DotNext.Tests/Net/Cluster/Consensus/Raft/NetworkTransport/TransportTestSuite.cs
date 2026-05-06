using System.Buffers;
using System.Collections.Immutable;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport;

using Buffers;
using IO;
using IO.Log;
using StateMachine;

public abstract class TransportTestSuite : RaftTest
{
    private sealed class BufferedEntry : TestTransferObject, IRaftLogEntry
    {
        internal BufferedEntry(long term, bool isSnapshot, byte[] content)
            : base(content)
        {
            Term = term;
            IsSnapshot = isSnapshot;
        }

        public long Term { get; }

        public bool IsSnapshot { get; }

    }

    public enum ReceiveEntriesBehavior
    {
        ReceiveAll = 0,
        ReceiveFirst,
        DropAll,
        DropFirst
    }

    private sealed class LocalMember : Assert, ILocalMember
    {
        internal readonly IList<BufferedEntry> ReceivedEntries = new List<BufferedEntry>();
        internal ReceiveEntriesBehavior Behavior;
        internal byte[] ReceivedConfiguration = [];
        internal long ReceivedConfigurationVersion = -1L;
        private readonly ClusterMemberId localId = Random.Shared.Next<ClusterMemberId>();

        internal LocalMember(bool smallAmountOfMetadata = false)
        {
            var metadata = ImmutableDictionary.CreateBuilder<string, string>();
            if (smallAmountOfMetadata)
                metadata.Add("a", "b");
            else
            {
                const string allowedChars = Alphabet + Numbers;
                for (var i = 0; i < 20; i++)
                    metadata.Add(string.Concat("key", i.ToString()), Random.Shared.GetString(allowedChars, 20));
            }
            Metadata = metadata.ToImmutableDictionary();
        }

        ref readonly ClusterMemberId ILocalMember.Id => ref localId;

        bool ILocalMember.IsLeader(IRaftClusterMember member) => throw new NotImplementedException();

        ValueTask<bool> ILocalMember.ResignAsync(CancellationToken token) => ValueTask.FromResult(true);

        async ValueTask<Result<HeartbeatResult>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            Equal(42L, senderTerm);
            Equal(1, prevLogIndex);
            Equal(56L, prevLogTerm);
            Equal(10, commitIndex);

            byte[] buffer;
            switch (Behavior)
            {
                case ReceiveEntriesBehavior.ReceiveAll:
                    while (await entries.MoveNextAsync())
                    {
                        True(entries.Current.Length.HasValue);
                        buffer = await entries.Current.ToByteArrayAsync(null, token);
                        ReceivedEntries.Add(new BufferedEntry(entries.Current.Term, entries.Current.IsSnapshot, buffer));
                    }
                    break;
                case ReceiveEntriesBehavior.DropAll:
                    break;
                case ReceiveEntriesBehavior.ReceiveFirst:
                    True(await entries.MoveNextAsync());
                    buffer = await entries.Current.ToByteArrayAsync(null, token);
                    ReceivedEntries.Add(new BufferedEntry(entries.Current.Term, entries.Current.IsSnapshot, buffer));
                    break;
                case ReceiveEntriesBehavior.DropFirst:
                    True(await entries.MoveNextAsync());
                    True(await entries.MoveNextAsync());
                    buffer = await entries.Current.ToByteArrayAsync(null, token);
                    ReceivedEntries.Add(new BufferedEntry(entries.Current.Term, entries.Current.IsSnapshot, buffer));
                    break;
            }

            return new()
            {
                Term = 43L,
                Value = HeartbeatResult.ReplicatedWithLeaderTerm,
            };
        }

        async ValueTask<Result<HeartbeatResult>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot,
            long snapshotIndex, CancellationToken token)
        {
            Equal(42L, senderTerm);
            Equal(10, snapshotIndex);
            True(snapshot.IsSnapshot);
            
            var buffer = await snapshot.ToByteArrayAsync(null, token);
            ReceivedEntries.Add(new BufferedEntry(snapshot.Term, snapshot.IsSnapshot, buffer));
            
            return new()
            {
                Term = 43L,
                Value = HeartbeatResult.ReplicatedWithLeaderTerm,
            };
        }

        async ValueTask<bool> ILocalMember.InstallConfigurationAsync<TConfiguration>(long senderTerm, TConfiguration configuration,
            long configurationVersion, CancellationToken token)
        {
            ReceivedConfigurationVersion = configurationVersion;
            ReceivedConfiguration = await configuration.ToByteArrayAsync(token: token).ConfigureAwait(false);
            return true;
        }

        ValueTask<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            True(token.CanBeCanceled);
            Equal(42L, term);
            Equal(1L, lastLogIndex);
            Equal(56L, lastLogTerm);
            return ValueTask.FromResult<Result<bool>>(new() { Term = 43L, Value = true });
        }

        ValueTask<Result<PreVoteResult>> ILocalMember.PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            True(token.CanBeCanceled);
            Equal(10L, term);
            Equal(2L, lastLogIndex);
            Equal(99L, lastLogTerm);
            return ValueTask.FromResult<Result<PreVoteResult>>(new() { Term = 44L, Value = PreVoteResult.Accepted });
        }

        ValueTask<long?> ILocalMember.SynchronizeAsync(long commitIndex, CancellationToken token)
        {
            Equal(long.MaxValue, commitIndex);
            return ValueTask.FromResult<long?>(42L);
        }

        public IReadOnlyDictionary<string, string> Metadata { get; }
    }

    private protected static MemoryAllocator<byte> DefaultAllocator => ArrayPool<byte>.Shared.ToAllocator();

    private protected delegate IServer ServerFactory(ILocalMember localMember, EndPoint address, TimeSpan timeout);
    private protected delegate RaftClusterMember ClientFactory(EndPoint address, ILocalMember localMember, TimeSpan timeout);

    private protected async Task RequestResponseTest(ServerFactory serverFactory, ClientFactory clientFactory)
    {
        var timeout = DefaultTimeout;
        //prepare server
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        var member = new LocalMember();
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);

        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);

        //Vote request
        var result = await client.As<IRaftClusterMember>().VoteAsync(42L, 1L, 56L, TestToken);
        True(result.Value);
        Equal(43L, result.Term);

        // PreVote request
        var preVote = await client.As<IRaftClusterMember>().PreVoteAsync(10L, 2L, 99L, TestToken);
        Equal(PreVoteResult.Accepted, preVote.Value);
        Equal(44L, preVote.Term);

        //Resign request
        True(await client.As<IRaftClusterMember>().ResignAsync(TestToken));

        //Heartbeat request
        var appendEntries = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, Array.Empty<BufferedEntry>(), 1L, 56L, 10L, TestToken);
        Equal(HeartbeatResult.ReplicatedWithLeaderTerm, appendEntries.Value);
        Equal(43L, appendEntries.Term);
    }

    private protected async Task StressTestCore(ServerFactory serverFactory, ClientFactory clientFactory)
    {
        var timeout = DefaultTimeout;
        //prepare server
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        var member = new LocalMember();
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);
        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);
        ICollection<Task<Result<bool>>> tasks = new LinkedList<Task<Result<bool>>>();
        for (var i = 0; i < 100; i++)
        {
            var task = client.As<IRaftClusterMember>().VoteAsync(42L, 1L, 56L, TestToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        All(tasks, static task =>
        {
            True(task.Result.Value);
            Equal(43L, task.Result.Term);
        });
    }

    private protected async Task MetadataRequestResponseTest(ServerFactory serverFactory, ClientFactory clientFactory, bool smallAmountOfMetadata)
    {
        var timeout = DefaultTimeout;
        //prepare server
        var member = new LocalMember(smallAmountOfMetadata);
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);

        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);
        Equal(member.Metadata, await client.As<IRaftClusterMember>().GetMetadataAsync(refresh: true, TestToken));
    }

    private static void Equal(in BufferedEntry x, in BufferedEntry y)
    {
        Equal(x.Term, y.Term);
        Equal(x.IsSnapshot, y.IsSnapshot);
        Equal(x.Content.Span, y.Content.Span);
    }

    private protected async Task SendingLogEntriesTest(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize, ReceiveEntriesBehavior behavior, bool useEmptyEntry)
    {
        var timeout = DefaultTimeout;
        var member = new LocalMember(false) { Behavior = behavior };

        //prepare server
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);

        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);
        byte[] buffer;
        if (useEmptyEntry)
        {
            buffer = [];
        }
        else
        {
            Random.Shared.NextBytes(buffer = new byte[533]);
        }

        var entry1 = new BufferedEntry(10L, false, buffer);
        buffer = new byte[payloadSize];
        Random.Shared.NextBytes(buffer);
        var entry2 = new BufferedEntry(11L, true, buffer);

        var result = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, [entry1, entry2], 1, 56, 10, TestToken);
        Equal(43L, result.Term);
        Equal(HeartbeatResult.ReplicatedWithLeaderTerm, result.Value);
        switch (behavior)
        {
            case ReceiveEntriesBehavior.ReceiveAll:
                Equal(2, member.ReceivedEntries.Count);
                Equal(entry1, member.ReceivedEntries[0]);
                Equal(entry2, member.ReceivedEntries[1]);
                break;
            case ReceiveEntriesBehavior.ReceiveFirst:
                Single(member.ReceivedEntries);
                Equal(entry1, member.ReceivedEntries[0]);
                break;
            case ReceiveEntriesBehavior.DropFirst:
                Single(member.ReceivedEntries);
                Equal(entry2, member.ReceivedEntries[0]);
                break;
            case ReceiveEntriesBehavior.DropAll:
                Empty(member.ReceivedEntries);
                break;
        }
    }

    private protected async Task SendingSnapshotTest(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize)
    {
        var timeout = DefaultTimeout;
        var member = new LocalMember(false);

        //prepare server
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);

        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);

        var payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);
        var snapshot = new BufferedEntry(10L, true, payload);

        var configuration = new BinaryTransferObject(ReadOnlyMemory<byte>.Empty);
        
        var result = await client.As<IRaftClusterMember>().InstallSnapshotAsync(42L, snapshot, 10L, configuration, 1L, TestToken);
        Equal(43L, result.Term);
        Equal(HeartbeatResult.ReplicatedWithLeaderTerm, result.Value);
        NotEmpty(member.ReceivedEntries);
        Equal(snapshot, member.ReceivedEntries[0]);

        Equal(1L, member.ReceivedConfigurationVersion);
        Equal(configuration.Content, member.ReceivedConfiguration);
    }

    private protected async Task SendingSnapshotAndEntriesAndConfiguration(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize, ReceiveEntriesBehavior behavior)
    {
        var timeout = DefaultTimeout;
        var member = new LocalMember(false) { Behavior = behavior };

        //prepare server
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);

        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);
        var payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);
        var entry1 = new BufferedEntry(10L, false, payload);
        payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);
        var entry2 = new BufferedEntry(11L, false, payload);

        var snapshot = new BufferedEntry(10L, true, payload);
        payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);
        var configuration = new BinaryTransferObject(payload);

        Result<HeartbeatResult> result;
        for (var i = 0; i < 100; i++)
        {
            // process snapshot
            result = await client.As<IRaftClusterMember>().InstallSnapshotAsync(42L, snapshot, 10L, configuration, 2L, TestToken);
            Equal(43L, result.Term);
            Equal(HeartbeatResult.ReplicatedWithLeaderTerm, result.Value);
            NotEmpty(member.ReceivedEntries);
            Equal(snapshot, member.ReceivedEntries[0]);
            member.ReceivedEntries.Clear();

            // process entries
            result = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, [entry1, entry2], 1, 56, 10, TestToken);
            Equal(43L, result.Term);
            Equal(HeartbeatResult.ReplicatedWithLeaderTerm, result.Value);
            switch (behavior)
            {
                case ReceiveEntriesBehavior.ReceiveAll:
                    Equal(2, member.ReceivedEntries.Count);
                    Equal(entry1, member.ReceivedEntries[0]);
                    Equal(entry2, member.ReceivedEntries[1]);
                    break;
                case ReceiveEntriesBehavior.ReceiveFirst:
                    Single(member.ReceivedEntries);
                    Equal(entry1, member.ReceivedEntries[0]);
                    break;
                case ReceiveEntriesBehavior.DropFirst:
                    Single(member.ReceivedEntries);
                    Equal(entry2, member.ReceivedEntries[0]);
                    break;
                case ReceiveEntriesBehavior.DropAll:
                    Empty(member.ReceivedEntries);
                    break;
            }

            Equal(configuration.Content, member.ReceivedConfiguration);
            Equal(2L, member.ReceivedConfigurationVersion);
            member.ReceivedEntries.Clear();
            member.ReceivedConfiguration = [];
            member.ReceivedConfigurationVersion = -1L;
        }
    }

    private protected async Task SendingSynchronizationRequestTest(ServerFactory serverFactory, ClientFactory clientFactory)
    {
        var timeout = DefaultTimeout;
        var member = new LocalMember(false);

        //prepare server
        var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
        await using var server = serverFactory(member, serverAddr, timeout);
        await server.StartAsync(TestToken);

        //prepare client
        using var client = clientFactory(serverAddr, member, timeout);
        Equal(42L, await client.As<IRaftClusterMember>().SynchronizeAsync(long.MaxValue, TestToken));
    }

    private protected async Task ClusterRecoveryCore(Func<int, bool, IPersistentState, RaftCluster> clusterFactory)
    {
        await using var wal2 = CreateWal();
        await using var host2 = clusterFactory(3271, false, wal2);
        
        await using var wal3 = CreateWal();
        await using var host3 = clusterFactory(3272, false, wal3);

        IClusterMember leader2, leader3;
        EndPoint oldLeader;
        await using (var wal1 = CreateWal())
        await using (var host1 = clusterFactory(3270, true, wal1))
        {
            await host1.StartAsync();
            True(host1.Readiness.IsCompletedSuccessfully);

            await host2.StartAsync();
            await host3.StartAsync();

            Equal(host1.LocalMemberAddress, (await host1.WaitForLeaderAsync(DefaultTimeout)).EndPoint);

            // add two nodes to the cluster
            await host1.AddMemberAsync(host2.LocalMemberAddress);
            await host2.Readiness.WaitAsync(DefaultTimeout);

            await host1.AddMemberAsync(host3.LocalMemberAddress);
            await host3.Readiness.WaitAsync(DefaultTimeout);

            oldLeader = await AssertLeadershipAsync(EqualityComparer<EndPoint>.Default, host1, host2, host3);

            // stop the leader
            await host1.StopAsync();
        }

        // wait for new election
        do
        {
            leader2 = await host2.WaitForLeaderAsync(DefaultTimeout);
            leader3 = await host3.WaitForLeaderAsync(DefaultTimeout);
        }
        while (object.Equals(oldLeader, leader2.EndPoint) || object.Equals(oldLeader, leader3.EndPoint));

        await host2.StopAsync();
        await host3.StopAsync();
    }

    private protected async Task LeadershipCore(Func<int, bool, IPersistentState, RaftCluster> clusterFactory)
    {
        // first node - cold start
        await using var wal1 = CreateWal();
        await using var host1 = clusterFactory(3267, true, wal1);
        var listener1 = new LeaderChangedEvent();
        host1.LeaderChanged += listener1.OnLeaderChanged;
        await host1.StartAsync();
        True(host1.Readiness.IsCompletedSuccessfully);

        // two nodes in frozen state
        await using var wal2 = CreateWal();
        await using var host2 = clusterFactory(3268, false, wal2);
        await host2.StartAsync();
        
        await using var wal3 = CreateWal();
        await using var host3 = clusterFactory(3269, false, wal3);
        await host3.StartAsync();

        await listener1.Task.WaitAsync(DefaultTimeout);
        Equal(host1.LocalMemberAddress, listener1.Task.Result.EndPoint);

        NotNull(host1.Leader);

        // force replication to renew the lease
        await host1.ForceReplicationAsync();
        True(host1.TryGetLeaseToken(out var leaseToken));
        False(leaseToken.IsCancellationRequested);
        False(host1.LeadershipToken.IsCancellationRequested);

        // add two nodes to the cluster
        True(await host1.AddMemberAsync(host2.LocalMemberAddress));
        await host2.Readiness.WaitAsync(DefaultTimeout);

        True(await host1.AddMemberAsync(host3.LocalMemberAddress));
        await host3.Readiness.WaitAsync(DefaultTimeout);

        await AssertLeadershipAsync(EqualityComparer<EndPoint>.Default, host1, host2, host3);

        foreach (var member in host1.Members)
        {
            var status = member.Status;
            if (status is not ClusterMemberStatus.Available)
                Fail($"Member {member.EndPoint} has unexpected status {status}");
        }

        await host3.StopAsync();
        await host2.StopAsync();
        await host1.StopAsync();
    }

    private static WriteAheadLog CreateWal()
        => new(new() { Location = GetTempPath() }, IStateMachine.CreateNoOp());
}