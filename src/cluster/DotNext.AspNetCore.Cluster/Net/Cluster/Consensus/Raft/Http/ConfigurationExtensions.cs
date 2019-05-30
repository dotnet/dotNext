using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public static class ConfigurationExtensions
    {
        [CLSCompliant(false)]
        public static IServiceCollection EnableCluster(this IServiceCollection services)
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<RaftHttpCluster>;
            return services.AddSingleton<RaftHttpCluster>()
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IMiddleware>(clusterNodeCast);
        }

        [CLSCompliant(false)]
        public static IServiceCollection EnableCluster(this IServiceCollection services, IConfiguration clusterConfig)
            => services.Configure<ClusterMemberConfiguration>(clusterConfig).EnableCluster();
    }
}
