using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;
    using Datagram;
    using IO;
    using IO.Log;
    using IClusterConfiguration = Membership.IClusterConfiguration;

    [ExcludeFromCodeCoverage]
    public abstract class TransportTestSuite : Test
    {
        private sealed class BufferedClusterConfiguration : BinaryTransferObject, IClusterConfiguration
        {
            internal BufferedClusterConfiguration(ReadOnlyMemory<byte> memory)
                : base(memory)
            {
            }

            public long Fingerprint { get; init; }

            long IClusterConfiguration.Length => Content.Length;
        }

        private sealed class BufferedEntry : BinaryTransferObject, IRaftLogEntry
        {
            internal BufferedEntry(long term, DateTimeOffset timestamp, bool isSnapshot, byte[] content)
                : base(content)
            {
                Term = term;
                Timestamp = timestamp;
                IsSnapshot = isSnapshot;
            }

            public long Term { get; }


            public DateTimeOffset Timestamp { get; }

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
            internal byte[] ReceivedConfiguration = Array.Empty<byte>();
            private readonly ClusterMemberId localId = Random.Shared.Next<ClusterMemberId>();

            internal LocalMember(bool smallAmountOfMetadata = false)
            {
                var metadata = ImmutableDictionary.CreateBuilder<string, string>();
                if (smallAmountOfMetadata)
                    metadata.Add("a", "b");
                else
                {
                    const string AllowedChars = "abcdefghijklmnopqrstuvwxyz1234567890";
                    for (var i = 0; i < 20; i++)
                        metadata.Add(string.Concat("key", i.ToString()), Random.Shared.NextString(AllowedChars, 20));
                }
                Metadata = metadata.ToImmutableDictionary();
            }

            ref readonly ClusterMemberId ILocalMember.Id => ref localId;

            bool ILocalMember.IsLeader(IRaftClusterMember member) => throw new NotImplementedException();

            ValueTask<bool> ILocalMember.ResignAsync(CancellationToken token) => ValueTask.FromResult(true);

            async ValueTask ILocalMember.ProposeConfigurationAsync(Func<Memory<byte>, CancellationToken, ValueTask> configurationReader, long configurationLength, long fingerprint, CancellationToken token)
            {
                using var buffer = MemoryAllocator.Allocate<byte>(configurationLength.Truncate(), true);
                await configurationReader(buffer.Memory, token).ConfigureAwait(false);
                Equal(42L, fingerprint);
                ReceivedConfiguration = buffer.Memory.ToArray();
            }

            private async ValueTask<Result<bool>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, long? fingerprint, bool applyConfig, CancellationToken token)
                where TEntry : IRaftLogEntry
            {
                Equal(42L, senderTerm);
                Equal(1, prevLogIndex);
                Equal(56L, prevLogTerm);
                Equal(10, commitIndex);

                if (fingerprint.HasValue)
                    Equal(42L, fingerprint.GetValueOrDefault());

                if (applyConfig)
                    Empty(ReceivedConfiguration);
                else
                    NotEmpty(ReceivedConfiguration);

                byte[] buffer;
                switch (Behavior)
                {
                    case ReceiveEntriesBehavior.ReceiveAll:
                        while (await entries.MoveNextAsync())
                        {
                            True(entries.Current.Length.HasValue);
                            buffer = await entries.Current.ToByteArrayAsync(null, token);
                            ReceivedEntries.Add(new BufferedEntry(entries.Current.Term, entries.Current.Timestamp, entries.Current.IsSnapshot, buffer));
                        }
                        break;
                    case ReceiveEntriesBehavior.DropAll:
                        break;
                    case ReceiveEntriesBehavior.ReceiveFirst:
                        True(await entries.MoveNextAsync());
                        buffer = await entries.Current.ToByteArrayAsync(null, token);
                        ReceivedEntries.Add(new BufferedEntry(entries.Current.Term, entries.Current.Timestamp, entries.Current.IsSnapshot, buffer));
                        break;
                    case ReceiveEntriesBehavior.DropFirst:
                        True(await entries.MoveNextAsync());
                        True(await entries.MoveNextAsync());
                        buffer = await entries.Current.ToByteArrayAsync(null, token);
                        ReceivedEntries.Add(new BufferedEntry(entries.Current.Term, entries.Current.Timestamp, entries.Current.IsSnapshot, buffer));
                        break;
                }

                return new Result<bool>(43L, true);
            }

            ValueTask<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, long? fingerprint, bool applyConfig, CancellationToken token)
                => AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, fingerprint, applyConfig, token);

            async ValueTask<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
            {
                if (config.Length > 0L)
                    ReceivedConfiguration = await config.ToByteArrayAsync(token: token);

                return await AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, config.Fingerprint, applyConfig, token);
            }

            async ValueTask<Result<bool>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            {
                Equal(42L, senderTerm);
                Equal(10, snapshotIndex);
                True(snapshot.IsSnapshot);
                var buffer = await snapshot.ToByteArrayAsync(null, token);
                ReceivedEntries.Add(new BufferedEntry(snapshot.Term, snapshot.Timestamp, snapshot.IsSnapshot, buffer));
                return new Result<bool>(43L, true);
            }

            ValueTask<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            {
                True(token.CanBeCanceled);
                Equal(42L, term);
                Equal(1L, lastLogIndex);
                Equal(56L, lastLogTerm);
                return ValueTask.FromResult(new Result<bool>(43L, true));
            }

            ValueTask<Result<PreVoteResult>> ILocalMember.PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            {
                True(token.CanBeCanceled);
                Equal(10L, term);
                Equal(2L, lastLogIndex);
                Equal(99L, lastLogTerm);
                return ValueTask.FromResult(new Result<PreVoteResult>(44L, PreVoteResult.Accepted));
            }

            ValueTask<long?> ILocalMember.SynchronizeAsync(long commitIndex, CancellationToken token)
            {
                Equal(long.MaxValue, commitIndex);
                return ValueTask.FromResult<long?>(42L);
            }

            public IReadOnlyDictionary<string, string> Metadata { get; }
        }

        private protected static Func<int, ExchangePool> ExchangePoolFactory(ILocalMember localMember)
        {
            ExchangePool CreateExchangePool(int count)
            {
                var result = new ExchangePool();
                while (--count >= 0)
                    result.Add(new ServerExchange(localMember));
                return result;
            }
            return CreateExchangePool;
        }

        private protected static Func<ServerExchange> ServerExchangeFactory(ILocalMember localMember)
            => () => new ServerExchange(localMember);

        private protected static MemoryAllocator<byte> DefaultAllocator => ArrayPool<byte>.Shared.ToAllocator();

        private protected delegate IServer ServerFactory(ILocalMember localMember, EndPoint address, TimeSpan timeout);
        private protected delegate RaftClusterMember ClientFactory(EndPoint address, ILocalMember localMember, TimeSpan timeout);

        private protected async Task RequestResponseTest(ServerFactory serverFactory, ClientFactory clientFactory)
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            var member = new LocalMember();
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);

            //Vote request
            var result = await client.As<IRaftClusterMember>().VoteAsync(42L, 1L, 56L, CancellationToken.None);
            True(result.Value);
            Equal(43L, result.Term);

            // PreVote request
            var preVote = await client.As<IRaftClusterMember>().PreVoteAsync(10L, 2L, 99L, CancellationToken.None);
            Equal(PreVoteResult.Accepted, preVote.Value);
            Equal(44L, preVote.Term);

            //Resign request
            True(await client.As<IRaftClusterMember>().ResignAsync(CancellationToken.None));

            //Heartbeat request
            var config = IClusterConfiguration.CreateEmpty(fingerprint: 42L);
            result = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, Array.Empty<BufferedEntry>(), 1L, 56L, 10L, config, true, CancellationToken.None);
            True(result.Value);
            Equal(43L, result.Term);
        }

        private protected async Task StressTestCore(ServerFactory serverFactory, ClientFactory clientFactory)
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            var member = new LocalMember();
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);
            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);
            ICollection<Task<Result<bool>>> tasks = new LinkedList<Task<Result<bool>>>();
            for (var i = 0; i < 100; i++)
            {
                var task = client.As<IRaftClusterMember>().VoteAsync(42L, 1L, 56L, CancellationToken.None);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                True(task.Result.Value);
                Equal(43L, task.Result.Term);
            }
        }

        private protected async Task MetadataRequestResponseTest(ServerFactory serverFactory, ClientFactory clientFactory, bool smallAmountOfMetadata)
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var member = new LocalMember(smallAmountOfMetadata);
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);
            Equal(member.Metadata, await client.As<IRaftClusterMember>().GetMetadataAsync(refresh: true, CancellationToken.None));
        }

        private static void Equal(in BufferedEntry x, in BufferedEntry y)
        {
            Equal(x.Term, y.Term);
            Equal(x.Timestamp, y.Timestamp);
            Equal(x.IsSnapshot, y.IsSnapshot);
            True(x.Content.IsSingleSegment);
            True(y.Content.IsSingleSegment);
            True(x.Content.FirstSpan.SequenceEqual(y.Content.FirstSpan));
        }

        private protected async Task SendingLogEntriesTest(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize, ReceiveEntriesBehavior behavior, bool useEmptyEntry)
        {
            var timeout = TimeSpan.FromSeconds(20);
            var member = new LocalMember(false) { Behavior = behavior };

            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);
            byte[] buffer;
            if (useEmptyEntry)
            {
                buffer = Array.Empty<byte>();
            }
            else
            {
                Random.Shared.NextBytes(buffer = new byte[533]);
            }

            var entry1 = new BufferedEntry(10L, DateTimeOffset.Now, false, buffer);
            buffer = new byte[payloadSize];
            Random.Shared.NextBytes(buffer);
            var entry2 = new BufferedEntry(11L, DateTimeOffset.Now, true, buffer);

            var config = IClusterConfiguration.CreateEmpty(fingerprint: 42L);
            var result = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, new[] { entry1, entry2 }, 1, 56, 10, config, true, CancellationToken.None);
            Equal(43L, result.Term);
            True(result.Value);
            switch (behavior)
            {
                case ReceiveEntriesBehavior.ReceiveAll:
                    Equal(2, member.ReceivedEntries.Count);
                    Equal(entry1, member.ReceivedEntries[0]);
                    Equal(entry2, member.ReceivedEntries[1]);
                    break;
                case ReceiveEntriesBehavior.ReceiveFirst:
                    Equal(1, member.ReceivedEntries.Count);
                    Equal(entry1, member.ReceivedEntries[0]);
                    break;
                case ReceiveEntriesBehavior.DropFirst:
                    Equal(1, member.ReceivedEntries.Count);
                    Equal(entry2, member.ReceivedEntries[0]);
                    break;
                case ReceiveEntriesBehavior.DropAll:
                    Empty(member.ReceivedEntries);
                    break;
            }
        }

        private protected async Task SendingSnapshotTest(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize)
        {
            var timeout = TimeSpan.FromSeconds(20);
            var member = new LocalMember(false);

            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);

            var buffer = new byte[payloadSize];
            Random.Shared.NextBytes(buffer);
            var snapshot = new BufferedEntry(10L, DateTimeOffset.Now, true, buffer);
            var result = await client.As<IRaftClusterMember>().InstallSnapshotAsync(42L, snapshot, 10L, CancellationToken.None);
            Equal(43L, result.Term);
            True(result.Value);
            NotEmpty(member.ReceivedEntries);
            Equal(snapshot, member.ReceivedEntries[0]);
        }

        private protected async Task SendingSnapshotAndEntriesAndConfiguration(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize, ReceiveEntriesBehavior behavior)
        {
            var timeout = TimeSpan.FromSeconds(20);
            var member = new LocalMember(false) { Behavior = behavior };

            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);
            var buffer = new byte[payloadSize];
            Random.Shared.NextBytes(buffer);
            var entry1 = new BufferedEntry(10L, DateTimeOffset.Now, false, buffer);
            buffer = new byte[payloadSize];
            Random.Shared.NextBytes(buffer);
            var entry2 = new BufferedEntry(11L, DateTimeOffset.Now, false, buffer);
            var config = new BufferedClusterConfiguration(RandomBytes(312)) { Fingerprint = 42L };

            var snapshot = new BufferedEntry(10L, DateTimeOffset.Now, true, buffer);

            Result<bool> result;
            for (var i = 0; i < 100; i++)
            {
                // process snapshot
                result = await client.As<IRaftClusterMember>().InstallSnapshotAsync(42L, snapshot, 10L, CancellationToken.None);
                Equal(43L, result.Term);
                True(result.Value);
                NotEmpty(member.ReceivedEntries);
                Equal(snapshot, member.ReceivedEntries[0]);
                member.ReceivedEntries.Clear();

                // process entries
                result = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, new[] { entry1, entry2 }, 1, 56, 10, config, false, CancellationToken.None);
                Equal(43L, result.Term);
                True(result.Value);
                switch (behavior)
                {
                    case ReceiveEntriesBehavior.ReceiveAll:
                        Equal(2, member.ReceivedEntries.Count);
                        Equal(entry1, member.ReceivedEntries[0]);
                        Equal(entry2, member.ReceivedEntries[1]);
                        break;
                    case ReceiveEntriesBehavior.ReceiveFirst:
                        Equal(1, member.ReceivedEntries.Count);
                        Equal(entry1, member.ReceivedEntries[0]);
                        break;
                    case ReceiveEntriesBehavior.DropFirst:
                        Equal(1, member.ReceivedEntries.Count);
                        Equal(entry2, member.ReceivedEntries[0]);
                        break;
                    case ReceiveEntriesBehavior.DropAll:
                        Empty(member.ReceivedEntries);
                        break;
                }

                True(member.ReceivedConfiguration.AsSpan().SequenceEqual(config.Content.FirstSpan));
                member.ReceivedEntries.Clear();
                member.ReceivedConfiguration = Array.Empty<byte>();
            }
        }

        private protected async Task SendingConfigurationTest(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize)
        {
            var timeout = TimeSpan.FromSeconds(20);
            var member = new LocalMember(false);

            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);

            var config = new BufferedClusterConfiguration(RandomBytes(payloadSize)) { Fingerprint = 42L };
            var result = await client.As<IRaftClusterMember>().AppendEntriesAsync<BufferedEntry, BufferedEntry[]>(42L, Array.Empty<BufferedEntry>(), 1L, 56L, 10L, config, payloadSize is 0, CancellationToken.None);

            True(member.ReceivedConfiguration.AsSpan().SequenceEqual(config.Content.FirstSpan));
        }

        private protected async Task SendingSynchronizationRequestTest(ServerFactory serverFactory, ClientFactory clientFactory)
        {
            var timeout = TimeSpan.FromSeconds(20);
            var member = new LocalMember(false);

            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            await using var server = serverFactory(member, serverAddr, timeout);
            await server.StartAsync(CancellationToken.None);

            //prepare client
            using var client = clientFactory(serverAddr, member, timeout);
            Equal(42L, await client.As<IRaftClusterMember>().SynchronizeAsync(long.MaxValue, CancellationToken.None));
        }

        private protected async Task ClusterRecoveryCore(Func<int, bool, RaftCluster> clusterFactory)
        {
            await using var host2 = clusterFactory(3271, false);
            await using var host3 = clusterFactory(3272, false);

            IClusterMember leader2, leader3;
            EndPoint oldLeader;
            await using (var host1 = clusterFactory(3270, true))
            {
                await host1.StartAsync();
                True(host1.Readiness.IsCompletedSuccessfully);

                await host2.StartAsync();
                await host3.StartAsync();

                Equal(host1.LocalMemberAddress, (await host1.WaitForLeaderAsync(DefaultTimeout)).EndPoint);

                // add two nodes to the cluster
                await host1.AddMemberAsync(host2.LocalMemberId, host2.LocalMemberAddress);
                await host2.Readiness.WaitAsync(DefaultTimeout);

                await host1.AddMemberAsync(host3.LocalMemberId, host3.LocalMemberAddress);
                await host3.Readiness.WaitAsync(DefaultTimeout);

                var leader1 = await host1.WaitForLeaderAsync(DefaultTimeout);
                leader2 = await host2.WaitForLeaderAsync(DefaultTimeout);
                leader3 = await host3.WaitForLeaderAsync(DefaultTimeout);
                Equal(leader1.EndPoint, leader2.EndPoint);
                Equal(leader1.EndPoint, leader3.EndPoint);
                False(host1.LeadershipToken.IsCancellationRequested);
                oldLeader = leader1.EndPoint;

                // stop the leader
                await host1.StopAsync();
            }

            // wait for new election
            do
            {
                leader2 = await host2.WaitForLeaderAsync(DefaultTimeout);
                leader3 = await host3.WaitForLeaderAsync(DefaultTimeout);
            }
            while (leader2 is null || leader3 is null || object.Equals(oldLeader, leader2.EndPoint) || object.Equals(oldLeader, leader3.EndPoint));

            await host2.StopAsync();
            await host3.StopAsync();
        }

        private protected async Task LeadershipCore(Func<int, bool, RaftCluster> clusterFactory)
        {
            // first node - cold start
            await using var host1 = clusterFactory(3267, true);
            var listener1 = new LeaderChangedEvent();
            host1.LeaderChanged += listener1.OnLeaderChanged;
            await host1.StartAsync();
            True(host1.Readiness.IsCompletedSuccessfully);

            // two nodes in frozen state
            await using var host2 = clusterFactory(3268, false);
            await host2.StartAsync();

            await using var host3 = clusterFactory(3269, false);
            await host3.StartAsync();

            await listener1.Result.WaitAsync(DefaultTimeout);
            Equal(host1.LocalMemberAddress, listener1.Result.Result.EndPoint);

            NotNull(host1.Leader);

            // force replication to renew the lease
            await host1.ForceReplicationAsync();
            NotNull(host1.Lease);
            False(host1.Lease.Token.IsCancellationRequested);
            False(host1.LeadershipToken.IsCancellationRequested);

            // add two nodes to the cluster
            True(await host1.AddMemberAsync(host2.LocalMemberId, host2.LocalMemberAddress));
            await host2.Readiness.WaitAsync(DefaultTimeout);

            True(await host1.AddMemberAsync(host3.LocalMemberId, host3.LocalMemberAddress));
            await host3.Readiness.WaitAsync(DefaultTimeout);

            Equal((await host1.WaitForLeaderAsync(DefaultTimeout)).EndPoint, (await host2.WaitForLeaderAsync(DefaultTimeout)).EndPoint);
            Equal((await host1.WaitForLeaderAsync(DefaultTimeout)).EndPoint, (await host3.WaitForLeaderAsync(DefaultTimeout)).EndPoint);

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }
    }
}