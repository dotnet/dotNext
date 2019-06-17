using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    public sealed class EmbeddedClusterSupportTests : ClusterMemberTest
    {
        [Fact]
        public static async Task SingleNode()
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
                await host.StopAsync();
            }
        }
    }
}
