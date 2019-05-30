using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using RaftHttpCluster = Http.RaftHttpCluster;

    public static class ConfigurationExtensions
    {
        private static IServiceCollection EnableCluster(this IServiceCollection services, Action<RaftCluster> initializer = null)
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<RaftHttpCluster>;
            return services.AddSingleton<RaftHttpCluster>()
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IMiddleware>(clusterNodeCast);
        }

        [CLSCompliant(false)]
        public static IServiceCollection EnableCluster(this IServiceCollection services, IConfiguration clusterConfig, Action<RaftCluster> initializer = null)
            => services.Configure<ClusterMemberConfiguration>(clusterConfig).EnableCluster();
        
        [CLSCompliant(false)]
        public static IServiceCollection ConfigureCluster<TConfig>(this IServiceCollection services)
            where TConfig : class, IRaftClusterConfigurer
            => services.AddSingleton<IRaftClusterConfigurer, TConfig>();
    }
}
