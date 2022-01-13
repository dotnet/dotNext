using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using TransportServices;

    [ExcludeFromCodeCoverage]
    [Collection(TestCollections.Raft)]
    public sealed class UdpSocketTests : TransportTestSuite
    {
        private static readonly IPEndPoint LocalHostRandomPort = new(IPAddress.Loopback, 0);
        private readonly Func<long> appIdGenerator = Random.Shared.Next<long>;

        [Fact]
        public Task RequestResponse()
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 2, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 2, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            return RequestResponseTest(CreateServer, CreateClient);
        }

        [Fact]
        public Task StressTest()
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };
            return StressTestTest(CreateServer, CreateClient);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task MetadataRequestResponse(bool smallAmountOfMetadata)
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            return MetadataRequestResponseTest(CreateServer, CreateClient, smallAmountOfMetadata);
        }

        [Theory]
        [InlineData(0, ReceiveEntriesBehavior.ReceiveAll, false)]
        [InlineData(0, ReceiveEntriesBehavior.ReceiveFirst, false)]
        [InlineData(0, ReceiveEntriesBehavior.DropAll, false)]
        [InlineData(0, ReceiveEntriesBehavior.DropFirst, false)]
        [InlineData(512, ReceiveEntriesBehavior.ReceiveAll, false)]
        [InlineData(512, ReceiveEntriesBehavior.ReceiveFirst, false)]
        [InlineData(512, ReceiveEntriesBehavior.DropAll, false)]
        [InlineData(512, ReceiveEntriesBehavior.DropFirst, false)]
        [InlineData(50, ReceiveEntriesBehavior.ReceiveAll, false)]
        [InlineData(50, ReceiveEntriesBehavior.ReceiveFirst, false)]
        [InlineData(50, ReceiveEntriesBehavior.DropAll, false)]
        [InlineData(50, ReceiveEntriesBehavior.DropFirst, false)]
        [InlineData(0, ReceiveEntriesBehavior.ReceiveAll, true)]
        [InlineData(0, ReceiveEntriesBehavior.ReceiveFirst, true)]
        [InlineData(0, ReceiveEntriesBehavior.DropAll, true)]
        [InlineData(0, ReceiveEntriesBehavior.DropFirst, true)]
        [InlineData(512, ReceiveEntriesBehavior.ReceiveAll, true)]
        [InlineData(512, ReceiveEntriesBehavior.ReceiveFirst, true)]
        [InlineData(512, ReceiveEntriesBehavior.DropAll, true)]
        [InlineData(512, ReceiveEntriesBehavior.DropFirst, true)]
        [InlineData(50, ReceiveEntriesBehavior.ReceiveAll, true)]
        [InlineData(50, ReceiveEntriesBehavior.ReceiveFirst, true)]
        [InlineData(50, ReceiveEntriesBehavior.DropAll, true)]
        [InlineData(50, ReceiveEntriesBehavior.DropFirst, true)]
        public Task SendingLogEntries(int payloadSize, ReceiveEntriesBehavior behavior, bool useEmptyEntry)
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };
            return SendingLogEntriesTest(CreateServer, CreateClient, payloadSize, behavior, useEmptyEntry);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingSnapshot(int payloadSize)
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            return SendingSnapshotTest(CreateServer, CreateClient, payloadSize);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingConfiguration(int payloadSize)
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            return SendingConfigurationTest(CreateServer, CreateClient, payloadSize);
        }

        [Fact]
        public Task RequestSynchronization()
        {
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };
            UdpClient CreateClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            return SendingSynchronizationRequestTest(CreateServer, CreateClient);
        }
    }
}