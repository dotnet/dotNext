using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using TransportServices;

    [ExcludeFromCodeCoverage]
    public sealed class TcpTransportTests : TransportTestSuite
    {
        [Fact]
        public static async Task ConnectionError()
        {
            using var client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 35665), ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
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
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new TcpServer(address, 2, ArrayPool<byte>.Shared, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 65535
            };
            static TcpClient CreateClient(IPEndPoint address) => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 65535
            };
            return RequestResponseTest(CreateServer, CreateClient);
        }

        [Fact]
        public Task StressTest()
        {
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 65535
            };
            static TcpClient CreateClient(IPEndPoint address) => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 65535
            };
            return StressTestTest(CreateServer, CreateClient);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task MetadataRequestResponse(bool smallAmountOfMetadata)
        {
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 300
            };
            static TcpClient CreateClient(IPEndPoint address) => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 300
            };
            return MetadataRequestResponseTest(CreateServer, CreateClient, smallAmountOfMetadata);
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
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 400,
                ReceiveTimeout = timeout,
            };
            static TcpClient CreateClient(IPEndPoint address) => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 400
            };
            return SendingLogEntriesTest(CreateServer, CreateClient, payloadSize, behavior);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingSnapshot(int payloadSize)
        {
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350,
                ReceiveTimeout = timeout,
            };
            static TcpClient CreateClient(IPEndPoint address) => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350
            };
            return SendingSnapshotTest(CreateServer, CreateClient, payloadSize);
        }
    }
}