using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using TransportServices;
    using TransportServices.Datagram;

    [ExcludeFromCodeCoverage]
    [Collection(TestCollections.Raft)]
    public sealed class UdpSocketTests : TransportTestSuite
    {
        private static readonly IPEndPoint LocalHostRandomPort = new(IPAddress.Loopback, 0);

        [Fact]
        public Task RequestResponse() => RequestResponseTest(
                CreateServerFactory(UdpSocket.MaxDatagramSize, false),
                CreateClientFactory(2, UdpSocket.MaxDatagramSize, false));

        [Fact]
        public Task StressTest() => StressTestCore(
                CreateServerFactory(UdpSocket.MaxDatagramSize, false),
                CreateClientFactory(100, UdpSocket.MaxDatagramSize, false));

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task MetadataRequestResponse(bool smallAmountOfMetadata) => MetadataRequestResponseTest(
                CreateServerFactory(UdpSocket.MinDatagramSize, true),
                CreateClientFactory(100, UdpSocket.MinDatagramSize, true),
                smallAmountOfMetadata);

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
        public Task SendingLogEntries(int payloadSize, ReceiveEntriesBehavior behavior, bool useEmptyEntry) => SendingLogEntriesTest(
                CreateServerFactory(UdpSocket.MinDatagramSize, true),
                CreateClientFactory(100, UdpSocket.MinDatagramSize, true),
                payloadSize,
                behavior,
                useEmptyEntry);

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingSnapshot(int payloadSize) => SendingSnapshotTest(
                CreateServerFactory(UdpSocket.MinDatagramSize, true),
                CreateClientFactory(100, UdpSocket.MinDatagramSize, true),
                payloadSize);

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        public Task SendingConfiguration(int payloadSize) => SendingConfigurationTest(
                CreateServerFactory(UdpSocket.MinDatagramSize, true),
                CreateClientFactory(100, UdpSocket.MinDatagramSize, true),
                payloadSize);

        [Fact]
        public Task RequestSynchronization() => SendingSynchronizationRequestTest(
                CreateServerFactory(UdpSocket.MinDatagramSize, true),
                CreateClientFactory(100, UdpSocket.MinDatagramSize, true));

        private static ServerFactory CreateServerFactory(int datagramSize, bool dontFragment)
        {
            return CreateServer;

            UdpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout)
            {
                var server = new UdpServer(address, 100, DefaultAllocator, ExchangePoolFactory(member), NullLoggerFactory.Instance)
                {
                    DatagramSize = datagramSize,
                    ReceiveTimeout = timeout,
                };

                if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsFreeBSD())
                    server.DontFragment = dontFragment;

                return server;
            }
        }

        private static ClientFactory CreateClientFactory(int backlog, int datagramSize, bool dontFragment)
        {
            return CreateClient;

            UdpClient CreateUdpClient(EndPoint address)
            {
                var client = new UdpClient(LocalHostRandomPort, address, backlog, DefaultAllocator, NullLoggerFactory.Instance)
                {
                    DatagramSize = datagramSize,
                };

                if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsFreeBSD())
                    client.DontFragment = dontFragment;

                return client;
            }

            ExchangePeer CreateClient(EndPoint address, ILocalMember member, TimeSpan requestTimeout)
                => new(member, address, Random.Shared.Next<ClusterMemberId>(), CreateUdpClient) { RequestTimeout = requestTimeout };
        }
    }
}