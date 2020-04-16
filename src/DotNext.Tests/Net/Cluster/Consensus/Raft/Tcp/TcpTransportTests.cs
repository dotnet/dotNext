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
            ServerFactory server = (member, address, timeout) => new TcpServer(address, 2, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 65535
            };
            ClientFactory client = address => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 65535
            };
            return RequestResponseTest(server, client);
        }

        [Fact]
        public Task StressTest()
        {
            ServerFactory server = (member, address, timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 65535
            };
            ClientFactory client = address => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 65535
            };
            return StressTestTest(server, client);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task MetadataRequestResponse(bool smallAmountOfMetadata)
        {
            ServerFactory server = (member, address, timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 300
            };
            ClientFactory client = address => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 300
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
            ServerFactory server = (member, address, timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 400,
                ReceiveTimeout = timeout,
            };
            ClientFactory client = address => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 400
            };
            return SendingLogEntriesTest(server, client, payloadSize, behavior);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingSnapshot(int payloadSize)
        {
            ServerFactory server = (member, address, timeout) => new TcpServer(address, 100, ArrayPool<byte>.Shared, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350,
                ReceiveTimeout = timeout,
            };
            ClientFactory client = address => new TcpClient(address, ArrayPool<byte>.Shared, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350
            };
            return SendingSnapshotTest(server, client, payloadSize);
        }

        private sealed class CompletionInfo
        {
            internal void OnCompleted(SocketAsyncEventArgs e)
            {

            }
        }

        [Fact]
        public static async Task UdpLocalHostRepro()
        {
            var localhost = IPAddress.Loopback;
            using var socket = new Socket(localhost.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(localhost, 3262));
            
            await socket.SendAsync(new byte[10], SocketFlags.None);
            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = new IPEndPoint(localhost, 3262);
            False(socket.ReceiveFromAsync(args));
            Equal(SocketError.ConnectionRefused, args.SocketError);
        }
    }
}