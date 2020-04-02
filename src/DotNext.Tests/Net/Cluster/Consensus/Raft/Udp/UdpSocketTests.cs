using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    [ExcludeFromCodeCoverage]
    public sealed class UdpSocketTests : Test
    {
        private sealed class SimpleServerExchangePool : Assert, IRaftRpcServer, IExchangePool
        {
            internal SimpleServerExchangePool()
            {
                var metadata = ImmutableDictionary.CreateBuilder<string, string>();
                var rnd = new Random();
                const string AllowedChars = "abcdefghijklmnopqrstuvwxyz1234567890";
                for(var i = 0; i < 20; i++)
                    metadata.Add(string.Concat("key", i.ToString()), rnd.NextString(AllowedChars, 20));
                Metadata = metadata.ToImmutableDictionary();
            }

            Task<bool> IRaftRpcServer.ResignAsync(CancellationToken token) => Task.FromResult(true);

            Task<Result<bool>> IRaftRpcServer.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            {
                True(token.CanBeCanceled);
                Equal(42L, term);
                Equal(1L, lastLogIndex);
                Equal(56L, lastLogTerm);
                return Task.FromResult(new Result<bool>(43L, true));
            }

            public bool TryRent(PacketHeaders headers, out IExchange exchange)
            {
                exchange = new ServerExchange(this);
                return true;
            }

            public IReadOnlyDictionary<string, string> Metadata { get; }

            void IExchangePool.Release(IExchange exchange)
                => ((ServerExchange)exchange).Reset();
        }

        [Fact]
        public static async Task ConnectionError()
        {
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 35665), 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            using var timeoutTokenSource = new CancellationTokenSource(500);
            client.Start();
            var exchange = new VoteExchange(10L, 20L, 30L);
            client.Enqueue(exchange, timeoutTokenSource.Token);
            switch(Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    await ThrowsAsync<SocketException>(() => exchange.Task);
                    break;
                case PlatformID.Win32NT:
                    await ThrowsAsync<TaskCanceledException>(() => exchange.Task);
                    break;
            }
            
        }

        [Fact]
        public static async Task VoteRequestResponse()
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = new UdpServer(serverAddr, 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            server.ReceiveTimeout = timeout;
            server.Start(new SimpleServerExchangePool());
            //prepare client
            using var client = new UdpClient(serverAddr, 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            using var timeoutTokenSource = new CancellationTokenSource(timeout);
            client.Start();
            var exchange = new VoteExchange(42L, 1L, 56L);
            client.Enqueue(exchange, timeoutTokenSource.Token);
            var result = await exchange.Task;
            True(result.Value);
            Equal(43L, result.Term);
            client.Stop();
            server.Stop();
        }

        [Fact]
        public static async Task StressTest()
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = new UdpServer(serverAddr, 100, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            server.ReceiveTimeout = timeout;
            server.Start(new SimpleServerExchangePool());
            //prepare client
            using var client = new UdpClient(serverAddr, 100, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            client.Start();
            ICollection<Task<Result<bool>>> tasks = new LinkedList<Task<Result<bool>>>();
            using(var timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                for(var i = 0; i < 100; i++)
                {
                    var exchange = new VoteExchange(42L, 1L, 56L);
                    client.Enqueue(exchange, timeoutTokenSource.Token);
                    tasks.Add(exchange.Task);
                }
                await Task.WhenAll(tasks);
            }
            foreach(var task in tasks)
            {
                True(task.Result.Value);
                Equal(43L, task.Result.Term);
            }
            client.Stop();
            server.Stop();
        }

        [Fact]
        public static async Task MetadataRequestResponse()
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = new UdpServer(serverAddr, 100, UdpSocket.MinDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            server.ReceiveTimeout = timeout;
            var exchangePool = new SimpleServerExchangePool();
            server.Start(exchangePool);
            //prepare client
            using var client = new UdpClient(serverAddr, 100, UdpSocket.MinDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            client.Start();
            var exchange = new MetadataExchange();
            client.Enqueue(exchange, default);
            var actual = new Dictionary<string, string>();
            await exchange.ReadAsync(actual, default);
            Equal(exchangePool.Metadata, actual);
            client.Stop();
            server.Stop();
        }

        [Fact]
        public static async Task ResignRequestResponse()
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = new UdpServer(serverAddr, 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            server.ReceiveTimeout = timeout;
            server.Start(new SimpleServerExchangePool());
            //prepare client
            using var client = new UdpClient(serverAddr, 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            client.Start();
            var exchange = new ResignExchange();
            client.Enqueue(exchange, default);
            True(await exchange.Task);
            client.Stop();
            server.Stop();
        }
    }
}