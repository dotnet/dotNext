using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.CustomTransport;

using TransportServices;
[Collection(TestCollections.Raft)]
public sealed class CustomTransportTests : TransportTestSuite
{
    [Fact]
    public Task RequestResponse() => RequestResponseTest(CreateServer, CreateClient);

    [Fact]
    public Task StressTest() => StressTestCore(CreateServer, CreateClient);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task MetadataRequestResponse(bool smallAmountOfMetadata)
        => MetadataRequestResponseTest(CreateServer, CreateClient, smallAmountOfMetadata);

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
        => SendingLogEntriesTest(CreateServer, CreateClient, payloadSize, behavior, useEmptyEntry);

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
    public Task SendingLogEntriesAndConfigurationAndSnapshot(int payloadSize, ReceiveEntriesBehavior behavior)
        => SendingSnapshotAndEntriesAndConfiguration(CreateServer, CreateClient, payloadSize, behavior);

    [Theory]
    [InlineData(512)]
    [InlineData(50)]
    [InlineData(0)]
    public Task SendingSnapshot(int payloadSize)
        => SendingSnapshotTest(CreateServer, CreateClient, payloadSize);

    [Theory]
    [InlineData(512)]
    [InlineData(50)]
    [InlineData(0)]
    public Task SendingConfiguration(int payloadSize)
        => SendingConfigurationTest(CreateServer, CreateClient, payloadSize);

    [Fact]
    public Task RequestSynchronization()
        => SendingSynchronizationRequestTest(CreateServer, CreateClient);

    private static GenericClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout)
        => new(member, address, CreateClientFactory(), DefaultAllocator) { ConnectTimeout = timeout };

    private static GenericServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout)
        => new(address, CreateServerFactory(), member, DefaultAllocator, NullLoggerFactory.Instance) { ReceiveTimeout = timeout };

    private static SocketTransportFactory CreateServerFactory()
    {
        var options = new SocketTransportOptions();
        return new(new OptionsWrapper<SocketTransportOptions>(options), NullLoggerFactory.Instance);
    }

    private static IConnectionFactory CreateClientFactory()
    {
        // https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Transport.Sockets/src/Client/SocketConnectionFactory.cs
        var factoryType = typeof(SocketTransportFactory).Assembly.GetType("Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory", throwOnError: true);
        var options = new SocketTransportOptions();

        return (IConnectionFactory)Activator.CreateInstance(factoryType, new OptionsWrapper<SocketTransportOptions>(options), NullLoggerFactory.Instance);
    }

    [Fact]
    public Task Leadership()
    {
        return LeadershipCore(CreateCluster);

        static RaftCluster CreateCluster(int port, bool coldStart)
        {
            var config = new RaftCluster.CustomTransportConfiguration(new IPEndPoint(IPAddress.Loopback, port), CreateServerFactory(), CreateClientFactory())
            {
                ColdStart = coldStart
            };

            return new(config);
        }
    }
}