using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using TransportServices;
using static DotNext.Extensions.Logging.TestLoggers;

[Collection(TestCollections.Raft)]
public sealed class TcpTransportTests : TransportTestSuite
{
    private static X509Certificate2 LoadCertificate()
    {
        using var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(Test), "node.pfx");
        using var ms = new MemoryStream(1024);
        rawCertificate?.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return new X509Certificate2(ms.ToArray(), "1234");
    }

    private static SslServerAuthenticationOptions CreateServerSslOptions() => new()
    {
        AllowRenegotiation = true,
        EncryptionPolicy = EncryptionPolicy.RequireEncryption,
        ServerCertificate = LoadCertificate()
    };

    private static SslClientAuthenticationOptions CreateClientSslOptions() => new()
    {
        AllowRenegotiation = true,
        TargetHost = "localhost",
        RemoteCertificateValidationCallback = ValidateCert
    };

    private static bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        => true;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task RequestResponse(bool useSsl)
    {
        TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 2, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            ReceiveTimeout = timeout,
            TransmissionBlockSize = 65535,
            GracefulShutdownTimeout = 2000,
            SslOptions = useSsl ? CreateServerSslOptions() : null
        };

        TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
            TransmissionBlockSize = 65535,
            SslOptions = useSsl ? CreateClientSslOptions() : null,
        };

        return RequestResponseTest(CreateServer, CreateClient);
    }

    [Fact]
    public Task StressTest()
    {
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            ReceiveTimeout = timeout,
            TransmissionBlockSize = 65535,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
            TransmissionBlockSize = 65535,
        };

        return StressTestCore(CreateServer, CreateClient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task MetadataRequestResponse(bool smallAmountOfMetadata)
    {
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            ReceiveTimeout = timeout,
            TransmissionBlockSize = 300,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
            TransmissionBlockSize = 300,
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
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            TransmissionBlockSize = 400,
            ReceiveTimeout = timeout,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            TransmissionBlockSize = 400,
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
        };

        return SendingLogEntriesTest(CreateServer, CreateClient, payloadSize, behavior, useEmptyEntry);
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
    public Task SendingLogEntriesAndConfigurationAndSnapshot(int payloadSize, ReceiveEntriesBehavior behavior)
    {
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            ReceiveTimeout = timeout,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
        };

        return SendingSnapshotAndEntriesAndConfiguration(CreateServer, CreateClient, payloadSize, behavior);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(50)]
    [InlineData(0)]
    public Task SendingSnapshot(int payloadSize)
    {
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            TransmissionBlockSize = 350,
            ReceiveTimeout = timeout,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
            TransmissionBlockSize = 350,
        };

        return SendingSnapshotTest(CreateServer, CreateClient, payloadSize);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(50)]
    [InlineData(0)]
    public Task SendingConfiguration(int payloadSize)
    {
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            TransmissionBlockSize = 350,
            ReceiveTimeout = timeout,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
            TransmissionBlockSize = 350,
        };

        return SendingConfigurationTest(CreateServer, CreateClient, payloadSize);
    }

    [Fact]
    public Task Leadership()
    {
        return LeadershipCore(CreateCluster);

        static RaftCluster CreateCluster(int port, bool coldStart)
        {
            var config = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, port)) { ColdStart = coldStart };
            return new(config);
        }
    }

    [Fact]
    public Task ClusterRecovery()
    {
        return ClusterRecoveryCore(CreateCluster);

        static RaftCluster CreateCluster(int port, bool coldStart)
        {
            var config = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, port)) { ColdStart = coldStart };
            return new(config);
        }
    }

    [Fact]
    public Task RequestSynchronization()
    {
        static TcpServer CreateServer(ILocalMember member, EndPoint address, TimeSpan timeout) => new(address, 100, member, DefaultAllocator, NullLoggerFactory.Instance)
        {
            TransmissionBlockSize = 350,
            ReceiveTimeout = timeout,
            GracefulShutdownTimeout = 2000
        };

        static TcpClient CreateClient(EndPoint address, ILocalMember member, TimeSpan timeout) => new(member, address, DefaultAllocator)
        {
            RequestTimeout = timeout,
            ConnectTimeout = timeout,
            TransmissionBlockSize = 350,
        };

        return SendingSynchronizationRequestTest(CreateServer, CreateClient);
    }

    [Fact]
    public static async Task ConcurrentElection()   // https://github.com/dotnet/dotNext/issues/168
    {
        const int host1Port = 3362;
        const int host2Port = 3363;
        const int host3Port = 3364;

        await using var host1 = new RaftCluster(CreateConfiguration(host1Port));
        await using var host2 = new RaftCluster(CreateConfiguration(host2Port));
        await using var host3 = new RaftCluster(CreateConfiguration(host3Port));

        // start in parallel
        await Task.WhenAll(
            Task.Run(() => host1.StartAsync()),
            Task.Run(() => host2.StartAsync()),
            Task.Run(() => host3.StartAsync()));

        var leaders = await Task.WhenAll(
            host1.WaitForLeaderAsync(DefaultTimeout),
            host2.WaitForLeaderAsync(DefaultTimeout),
            host3.WaitForLeaderAsync(DefaultTimeout));

        var leader = default(EndPoint);

        foreach (var member in leaders)
        {
            if (leader is null)
            {
                leader = member.EndPoint;
            }
            else
            {
                Equal(leader, member.EndPoint);
            }
        }

        await host1.StopAsync();
        await host2.StopAsync();
        await host3.StopAsync();

        static RaftCluster.TcpConfiguration CreateConfiguration(int port)
        {
            var result = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, port))
            {
                // LowerElectionTimeout = 1000,
                // UpperElectionTimeout = 2000,
                ColdStart = false,
                LoggerFactory = CreateDebugLoggerFactory(port.ToString(), static builder => builder.SetMinimumLevel(LogLevel.Debug)),
            };

            var builder = result.UseInMemoryConfigurationStorage().CreateActiveConfigurationBuilder();

            builder.Add(new IPEndPoint(IPAddress.Loopback, host1Port));
            builder.Add(new IPEndPoint(IPAddress.Loopback, host2Port));
            builder.Add(new IPEndPoint(IPAddress.Loopback, host3Port));

            builder.Build();
            return result;
        }
    }
}