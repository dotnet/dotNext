using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    public sealed class EmbeddedClusterSupportTests : ClusterMemberTest
    {
        private sealed class LeaderChangedEvent : ManualResetEventSlim, IRaftClusterConfigurator
        {
            internal volatile IClusterMember Leader;

            internal LeaderChangedEvent()
                : base(false)
            {

            }

            void IRaftClusterConfigurator.Initialize(IRaftCluster cluster, IDictionary<string, string> metadata)
            {
                Equal("TestNode", metadata["nodeName"]);
                cluster.LeaderChanged += OnLeaderChanged;
            }

            private void OnLeaderChanged(ICluster sender, IClusterMember leader)
            {
                Set();
                Leader = leader;
            }

            void IRaftClusterConfigurator.Shutdown(IRaftCluster cluster)
            {
                cluster.LeaderChanged -= OnLeaderChanged;
            }
        }

        [Fact]
        public static async Task SingleNodeWithoutConsensus()
        {
            var config = new Dictionary<string, string>()
            {
                { "absoluteMajority", "true" },
                { "metadata:nodeName", "TestNode" },
                { "members:0", "http://localhost:3262" },
                { "members:1", "http://localhost:3263" }
            };
            using (var leaderResetEvent = new LeaderChangedEvent())
            using (var host = CreateHost<WebApplicationSetup>(3262, true, config, leaderResetEvent))
            {
                await host.StartAsync();
                leaderResetEvent.Wait(2000);
                Null(leaderResetEvent.Leader);
                await host.StopAsync();
            }
        }

        [Fact]
        public static async Task SingleNodeWithConsensus()
        {
            var config = new Dictionary<string, string>()
            {
                { "absoluteMajority", "false" },
                { "metadata:nodeName", "TestNode" },
                { "members:0", "http://localhost:3262" },
                { "members:1", "http://localhost:3263" }
            };
            using(var leaderResetEvent = new LeaderChangedEvent())
            using (var host = CreateHost<WebApplicationSetup>(3262, true, config, leaderResetEvent))
            {
                await host.StartAsync();
                leaderResetEvent.Wait(2000);
                NotNull(leaderResetEvent.Leader);
                False(leaderResetEvent.Leader.IsRemote);
                Equal("TestNode", (await leaderResetEvent.Leader.GetMetadata())["nodeName"]);
                await host.StopAsync();
            }
        }

        [Fact]
        public static async Task DependencyInjection()
        {
            var config = new Dictionary<string, string>()
            {
                { "absoluteMajority", "true" },
                { "metadata:nodeName", "TestNode" },
                { "members:0", "http://localhost:3262" },
                { "members:1", "http://localhost:3263" },
                { "allowedNetworks:0", "127.0.0.0" }
            };
            using (var host = CreateHost<WebApplicationSetup>(3262, true, config))
            {
                await host.StartAsync();
                object service = host.Services.GetService<ICluster>();
                NotNull(service);
                service = host.Services.GetService<IExpandableCluster>();
                NotNull(service);
                service = host.Services.GetService<IRaftCluster>();
                NotNull(service);
                await host.StopAsync();
            }
        }
    }
}
