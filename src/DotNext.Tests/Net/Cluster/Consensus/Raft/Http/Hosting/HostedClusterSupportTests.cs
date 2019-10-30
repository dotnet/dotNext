using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    [ExcludeFromCodeCoverage]
    public sealed class HostedClusterSupportTests : ClusterMemberTest
    {
        [Fact]
        public static async Task DependencyInjection()
        {
            var config = new Dictionary<string, string>()
            {
                {"partitioning", "false"},
                {"metadata:nodeName", "TestNode"},
                {"port", "3565"},
                {"members:0", "http://localhost:3565"},
                {"members:1", "http://localhost:3566"},
                {"allowedNetworks:0", "127.0.0.0"}
            };
            using (var host = CreateHost<WebApplicationSetup>(3100, true, config))
            {
                await host.StartAsync();
                object service = host.Services.GetService<ICluster>();
                NotNull(service);
                //check whether the local member present
                var count = 0;
                foreach (var member in host.Services.GetService<ICluster>().Members)
                    if (!member.IsRemote)
                        count += 1;
                Equal(1, count);
                service = host.Services.GetService<IExpandableCluster>();
                NotNull(service);
                service = host.Services.GetService<IRaftCluster>();
                NotNull(service);
                await host.StopAsync();
            }
        }
    }
}
