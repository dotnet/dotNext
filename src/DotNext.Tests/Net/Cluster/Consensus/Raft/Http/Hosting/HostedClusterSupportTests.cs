using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    public sealed class HostedClusterSupportTests : ClusterMemberTest
    {
        [Fact]
        public static async Task DependencyInjection()
        {
            var config = new Dictionary<string, string>()
            {
                {"partitioning", "false"},
                {"metadata:nodeName", "TestNode"},
                {"port", "3262"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"allowedNetworks:0", "127.0.0.0"}
            };
            using (var host = CreateHost<WebApplicationSetup>(3100, true, config))
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
