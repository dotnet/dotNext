using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;
    using IO;
    using IO.Log;

    [ExcludeFromCodeCoverage]
    public abstract class TransportTestSuite : Test
    {
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

            internal LocalMember(bool smallAmountOfMetadata = false)
            {
                var metadata = ImmutableDictionary.CreateBuilder<string, string>();
                if (smallAmountOfMetadata)
                    metadata.Add("a", "b");
                else
                {
                    var rnd = new Random();
                    const string AllowedChars = "abcdefghijklmnopqrstuvwxyz1234567890";
                    for (var i = 0; i < 20; i++)
                        metadata.Add(string.Concat("key", i.ToString()), rnd.NextString(AllowedChars, 20));
                }
                Metadata = metadata.ToImmutableDictionary();
            }

            ref readonly ClusterMemberId ILocalMember.Id => throw new NotImplementedException();

            bool ILocalMember.IsLeader(IRaftClusterMember member) => throw new NotImplementedException();

            Task<bool> ILocalMember.ResignAsync(CancellationToken token) => Task.FromResult(true);

            async Task<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
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

            async Task<Result<bool>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            {
                Equal(42L, senderTerm);
                Equal(10, snapshotIndex);
                True(snapshot.IsSnapshot);
                var buffer = await snapshot.ToByteArrayAsync(null, token);
                ReceivedEntries.Add(new BufferedEntry(snapshot.Term, snapshot.Timestamp, snapshot.IsSnapshot, buffer));
                return new Result<bool>(43L, true);
            }

            Task<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            {
                True(token.CanBeCanceled);
                Equal(42L, term);
                Equal(1L, lastLogIndex);
                Equal(56L, lastLogTerm);
                return Task.FromResult(new Result<bool>(43L, true));
            }

            Task<Result<bool>> ILocalMember.PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            {
                True(token.CanBeCanceled);
                Equal(10L, term);
                Equal(2L, lastLogIndex);
                Equal(99L, lastLogTerm);
                return Task.FromResult(new Result<bool>(44L, true));
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

        private protected delegate IServer ServerFactory(ILocalMember localMember, IPEndPoint address, TimeSpan timeout);
        private protected delegate IClient ClientFactory(IPEndPoint address);

        private protected async Task RequestResponseTest(ServerFactory serverFactory, ClientFactory clientFactory)
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = serverFactory(new LocalMember(), serverAddr, timeout);
            server.Start();
            //prepare client
            using var client = clientFactory(serverAddr);
            //Vote request
            CancellationTokenSource timeoutTokenSource;
            Result<bool> result;
            using (timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                var exchange = new VoteExchange(42L, 1L, 56L);
                client.Enqueue(exchange, timeoutTokenSource.Token);
                result = await exchange.Task;
                True(result.Value);
                Equal(43L, result.Term);
            }
            // PreVote reqyest
            using (timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                var exchange = new PreVoteExchange(10L, 2L, 99L);
                client.Enqueue(exchange, timeoutTokenSource.Token);
                result = await exchange.Task;
                True(result.Value);
                Equal(44L, result.Term);
            }
            //Resign request
            using (timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                var exchange = new ResignExchange();
                client.Enqueue(exchange, timeoutTokenSource.Token);
                True(await exchange.Task);
            }
            //Heartbeat request
            using (timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                var exchange = new HeartbeatExchange(42L, 1L, 56L, 10L);
                client.Enqueue(exchange, timeoutTokenSource.Token);
                result = await exchange.Task;
                True(result.Value);
                Equal(43L, result.Term);
            }
        }

        private protected async Task StressTestTest(ServerFactory serverFactory, ClientFactory clientFactory)
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = serverFactory(new LocalMember(), serverAddr, timeout);
            server.Start();
            //prepare client
            using var client = clientFactory(serverAddr);
            ICollection<Task<Result<bool>>> tasks = new LinkedList<Task<Result<bool>>>();
            using (var timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                for (var i = 0; i < 100; i++)
                {
                    var exchange = new VoteExchange(42L, 1L, 56L);
                    client.Enqueue(exchange, timeoutTokenSource.Token);
                    tasks.Add(exchange.Task);
                }
                await Task.WhenAll(tasks);
            }
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
            using var server = serverFactory(member, serverAddr, timeout);
            server.Start();
            //prepare client
            using var client = clientFactory(serverAddr);
            var exchange = new MetadataExchange(CancellationToken.None);
            client.Enqueue(exchange, default);
            Equal(member.Metadata, await exchange.Task);
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

        private protected async Task SendingLogEntriesTest(ServerFactory serverFactory, ClientFactory clientFactory, int payloadSize, ReceiveEntriesBehavior behavior)
        {
            var timeout = TimeSpan.FromSeconds(20);
            using var timeoutTokenSource = new CancellationTokenSource(timeout);
            var member = new LocalMember(false) { Behavior = behavior };
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = serverFactory(member, serverAddr, timeout);
            server.Start();
            //prepare client
            using var client = clientFactory(serverAddr);
            var buffer = new byte[533];
            var rnd = new Random();
            rnd.NextBytes(buffer);
            var entry1 = new BufferedEntry(10L, DateTimeOffset.Now, false, buffer);
            buffer = new byte[payloadSize];
            rnd.NextBytes(buffer);
            var entry2 = new BufferedEntry(11L, DateTimeOffset.Now, true, buffer);

            await using var exchange = new EntriesExchange<BufferedEntry, BufferedEntry[]>(42L, new[] { entry1, entry2 }, 1, 56, 10);
            client.Enqueue(exchange, timeoutTokenSource.Token);
            var result = await exchange.Task;
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
            using var timeoutTokenSource = new CancellationTokenSource(timeout);
            var member = new LocalMember(false);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = serverFactory(member, serverAddr, timeout);
            server.Start();
            //prepare client
            using var client = clientFactory(serverAddr);
            var buffer = new byte[payloadSize];
            new Random().NextBytes(buffer);
            var snapshot = new BufferedEntry(10L, DateTimeOffset.Now, true, buffer);
            await using var exchange = new SnapshotExchange(42L, snapshot, 10L);
            client.Enqueue(exchange, timeoutTokenSource.Token);
            var result = await exchange.Task;
            Equal(43L, result.Term);
            True(result.Value);
            NotEmpty(member.ReceivedEntries);
            Equal(snapshot, member.ReceivedEntries[0]);
        }
    }
}