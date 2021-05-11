using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using TransportServices;

    [ExcludeFromCodeCoverage]
    public sealed class TcpTransportTests : TransportTestSuite
    {
        private sealed class LeaderChangedEvent : EventWaitHandle
        {
            internal volatile IClusterMember Leader;

            internal LeaderChangedEvent()
                : base(false, EventResetMode.ManualReset)
            {
            }

            internal void OnLeaderChanged(ICluster sender, IClusterMember leader)
            {
                if (leader is null)
                    return;
                Leader = leader;
                Set();
            }
        }

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
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(300)]
        [InlineData(400)]
        [InlineData(500)]
        public async Task Leadership(int delay)
        {
            static void CheckLeadership(IClusterMember member1, IClusterMember member2)
                => Equal(member1.EndPoint, member2.EndPoint);

            static void AddMembers(RaftCluster.NodeConfiguration config)
            {
                config.Members.Add(new IPEndPoint(IPAddress.Loopback, 3267));
                config.Members.Add(new IPEndPoint(IPAddress.Loopback, 3268));
                config.Members.Add(new IPEndPoint(IPAddress.Loopback, 3269));
            }

            var config1 = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, 3267));
            AddMembers(config1);
            var config2 = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, 3268));
            AddMembers(config2);
            var config3 = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, 3269));
            AddMembers(config3);

            using var listener1 = new LeaderChangedEvent();
            using var listener2 = new LeaderChangedEvent();
            using var listener3 = new LeaderChangedEvent();

            using var host1 = new RaftCluster(config1);
            host1.LeaderChanged += listener1.OnLeaderChanged;
            using var host2 = new RaftCluster(config2);
            host2.LeaderChanged += listener2.OnLeaderChanged;
            using var host3 = new RaftCluster(config3);
            host3.LeaderChanged += listener3.OnLeaderChanged;

            await host1.StartAsync();
            await host2.StartAsync();
            await Task.Delay(delay);
            await host3.StartAsync();

            WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2, listener3 }, DefaultTimeout);

            IClusterMember leader1, leader2, leader3;

            //wait for stable election
            for (var timer = Task.Delay(2000); ; await Task.Delay(100))
            {
                if (timer.IsCompleted)
                    throw new RaftProtocolException("Leader election failed");
                leader1 = host1.Leader;
                leader2 = host2.Leader;
                leader3 = host3.Leader;
                if (leader1 is null || leader2 is null || leader3 is null)
                    continue;
                if (leader1.EndPoint.Equals(leader2.EndPoint) && leader1.EndPoint.Equals(leader2.EndPoint))
                    break;
            }

            listener1.Reset();
            listener2.Reset();
            listener3.Reset();
            listener1.Leader = listener2.Leader = listener3.Leader = null;

            //let's shutdown leader node

            var removedNode = default(int?);

            if (!leader1.IsRemote)
            {
                removedNode = 1;
                await host1.StopAsync();
            }

            if (!leader2.IsRemote)
            {
                removedNode = 2;
                await host2.StopAsync();
            }

            if (!leader3.IsRemote)
            {
                removedNode = 3;
                await host3.StopAsync();
            }

            NotNull(removedNode);

            switch (removedNode)
            {
                case 1:
                    //wait for new leader
                    WaitHandle.WaitAll(new WaitHandle[] { listener2, listener3 }, DefaultTimeout);
                    NotNull(listener2.Leader);
                    NotNull(listener3.Leader);
                    CheckLeadership(listener2.Leader, listener3.Leader);
                    break;
                case 2:
                    //wait for new leader
                    WaitHandle.WaitAll(new WaitHandle[] { listener1, listener3 }, DefaultTimeout);
                    NotNull(listener1.Leader);
                    NotNull(listener3.Leader);
                    CheckLeadership(listener1.Leader, listener3.Leader);
                    break;
                case 3:
                    //wait for new leader
                    WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2 }, DefaultTimeout);
                    NotNull(listener1.Leader);
                    NotNull(listener2.Leader);
                    CheckLeadership(listener1.Leader, listener2.Leader);
                    break;
                default:
                    throw new Exception();
            }

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }
    }
}