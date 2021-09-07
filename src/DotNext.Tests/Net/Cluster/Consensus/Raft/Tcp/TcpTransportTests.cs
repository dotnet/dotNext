using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using TransportServices;

    [ExcludeFromCodeCoverage]
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
            TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 2, DefaultAllocator, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 65535,
                GracefulShutdownTimeout = 2000,
                SslOptions = useSsl ? CreateServerSslOptions() : null
            };

            TcpClient CreateClient(IPEndPoint address) => new(address, DefaultAllocator, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 65535,
                SslOptions = useSsl ? CreateClientSslOptions() : null
            };
            return RequestResponseTest(CreateServer, CreateClient);
        }

        [Fact]
        public Task StressTest()
        {
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 65535,
                GracefulShutdownTimeout = 2000
            };
            static TcpClient CreateClient(IPEndPoint address) => new(address, DefaultAllocator, NullLoggerFactory.Instance)
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
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                ReceiveTimeout = timeout,
                TransmissionBlockSize = 300,
                GracefulShutdownTimeout = 2000
            };
            static TcpClient CreateClient(IPEndPoint address) => new(address, DefaultAllocator, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 300
            };
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
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 400,
                ReceiveTimeout = timeout,
                GracefulShutdownTimeout = 2000
            };
            static TcpClient CreateClient(IPEndPoint address) => new(address, DefaultAllocator, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 400
            };
            return SendingLogEntriesTest(CreateServer, CreateClient, payloadSize, behavior);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        [InlineData(0)]
        public Task SendingSnapshot(int payloadSize)
        {
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350,
                ReceiveTimeout = timeout,
                GracefulShutdownTimeout = 2000
            };
            static TcpClient CreateClient(IPEndPoint address) => new(address, DefaultAllocator, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350
            };

            return SendingSnapshotTest(CreateServer, CreateClient, payloadSize);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(50)]
        [InlineData(0)]
        public Task SendingConfiguration(int payloadSize)
        {
            static TcpServer CreateServer(ILocalMember member, IPEndPoint address, TimeSpan timeout) => new(address, 100, DefaultAllocator, ServerExchangeFactory(member), NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350,
                ReceiveTimeout = timeout,
                GracefulShutdownTimeout = 2000
            };
            static TcpClient CreateClient(IPEndPoint address) => new(address, DefaultAllocator, NullLoggerFactory.Instance)
            {
                TransmissionBlockSize = 350
            };

            return SendingConfigurationTest(CreateServer, CreateClient, payloadSize);
        }

        private static RaftCluster CreateCluster(int port, bool coldStart)
        {
            var config = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, port)) { ColdStart = coldStart };
            return new(config);
        }

        [Fact]
        public async Task Leadership()
        {
            // first node - cold start
            await using var host1 = CreateCluster(3267, true);
            var listener1 = new LeaderChangedEvent();
            host1.LeaderChanged += listener1.OnLeaderChanged;
            await host1.StartAsync();
            True(host1.Readiness.IsCompletedSuccessfully);

            // two nodes in frozen state
            await using var host2 = CreateCluster(3268, false);
            await host2.StartAsync();

            await using var host3 = CreateCluster(3269, false);
            await host3.StartAsync();

            await listener1.Result.WaitAsync(DefaultTimeout);
            Equal(host1.LocalMemberAddress, listener1.Result.Result.EndPoint);

            // add two nodes to the cluster
            True(await host1.AddMemberAsync(host2.LocalMemberId, host2.LocalMemberAddress));
            await host2.Readiness.WaitAsync(DefaultTimeout);

            True(await host1.AddMemberAsync(host3.LocalMemberId, host3.LocalMemberAddress));
            await host3.Readiness.WaitAsync(DefaultTimeout);

            Equal(host1.Leader.EndPoint, host2.Leader.EndPoint);
            Equal(host1.Leader.EndPoint, host3.Leader.EndPoint);

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }
    }
}