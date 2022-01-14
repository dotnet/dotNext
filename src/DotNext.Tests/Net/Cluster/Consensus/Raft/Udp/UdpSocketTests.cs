using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using TransportServices;
    using TransportServices.Datagram;

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

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 2, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

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

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MaxDatagramSize,
                DontFragment = false
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

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

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

            return MetadataRequestResponseTest(CreateServer, CreateClient, smallAmountOfMetadata);
        }

        [Theory]
        [InlineData(0, ReceiveEntriesBehavior.ReceiveAll)]
        [InlineData(0, ReceiveEntriesBehavior.ReceiveFirst)]
        [InlineData(0, ReceiveEntriesBehavior.DropAll)]
        [InlineData(0, ReceiveEntriesBehavior.DropFirst)]
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
            static UdpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                ReceiveTimeout = timeout,
                DontFragment = true
            };

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

            return SendingLogEntriesTest(CreateServer, CreateClient, payloadSize, behavior);
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

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

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

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

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

            UdpClient CreateUdpClient(IPEndPoint address) => new(LocalHostRandomPort, address, 100, DefaultAllocator, appIdGenerator, NullLoggerFactory.Instance)
            {
                DatagramSize = UdpSocket.MinDatagramSize,
                DontFragment = true
            };

            ExchangePeer CreateClient(IPEndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout, IsRemote = true };

            return SendingSynchronizationRequestTest(CreateServer, CreateClient);
        }
    }
}