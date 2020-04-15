using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using TransportServices;

    [ExcludeFromCodeCoverage]
    public sealed class UdpSocketTests : TransportTestSuite
    {
        private readonly Func<long> appIdGenerator = new Random().Next<long>;

        [Fact]
        public async Task ConnectionError()
        {
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 35665), 2, ArrayPool<byte>.Shared, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            using var timeoutTokenSource = new CancellationTokenSource(500);
            var exchange = new VoteExchange(10L, 20L, 30L);
            client.Enqueue(exchange, timeoutTokenSource.Token);
            switch (Environment.OSVersion.Platform)
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
        public Task RequestResponse()
        {
            ServerFactory server = (member, address, timeout) => new UdpServer(address, 2, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            ClientFactory client = address => new UdpClient(address, 2, ArrayPool<byte>.Shared, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            return RequestResponseTest(server, client);
        }

        [Fact]
        public Task StressTest()
        {
            ServerFactory server = (member, address, timeout) => new UdpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            ClientFactory client = address => new UdpClient(address, 100, ArrayPool<byte>.Shared, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            return StressTestTest(server, client);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task MetadataRequestResponse(bool smallAmountOfMetadata)
        {
            ServerFactory server = (member, address, timeout) => new UdpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            ClientFactory client = address => new UdpClient(address, 100, ArrayPool<byte>.Shared, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            return MetadataRequestResponseTest(server, client, smallAmountOfMetadata);
        }

        [Theory]
        [InlineData(512, ReceiveEntriesBehavior.ReceiveAll)]
        [InlineData(512, ReceiveEntriesBehavior.ReceiveFirst)]
        [InlineData(512, ReceiveEntriesBehavior.DropAll)]
        [InlineData(512, ReceiveEntriesBehavior.DropFirst)]
        [InlineData(50, ReceiveEntriesBehavior.ReceiveAll)]
        [InlineData(50, ReceiveEntriesBehavior.ReceiveFirst)]
        [InlineData(50, ReceiveEntriesBehavior.DropAll)]
        [InlineData(50, ReceiveEntriesBehavior.DropFirst)]
        public Task SendingLogEntries(int payloadSize, ReceiveEntriesBehavior behavior)
        {
            ServerFactory server = (member, address, timeout) => new UdpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };
            ClientFactory client = address => new UdpClient(address, 100, ArrayPool<byte>.Shared, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            return SendingLogEntriesTest(server, client, payloadSize, behavior);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingSnapshot(int payloadSize)
        {
            ServerFactory server = (member, address, timeout) => new UdpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };
            ClientFactory client = address => new UdpClient(address, 100, ArrayPool<byte>.Shared, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            return SendingSnapshotTest(server, client, payloadSize);
        }
    }
}