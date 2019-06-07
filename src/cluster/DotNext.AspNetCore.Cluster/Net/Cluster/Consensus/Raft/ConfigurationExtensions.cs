using System;
using DotNext.Net.Cluster.Consensus.Raft.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    [CLSCompliant(false)]
    public static class ConfigurationExtensions
    {
        private static IServiceCollection EnableCluster(this IServiceCollection services)
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<RaftHttpCluster>;
            return services
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IRaftCluster>(clusterNodeCast)
                .AddSingleton<IExpandableCluster>(clusterNodeCast);
        }

        public static IServiceCollection EmbedClusterSupport(this IServiceCollection services, IConfiguration clusterConfig)
            => services.Configure<ClusterMemberConfiguration>(clusterConfig).AddSingleton<RaftHttpCluster>().EnableCluster();

        /// <summary>
        /// Registers configurator of <see cref="ICluster"/> service registered as a service
        /// in DI container.
        /// </summary>
        /// <typeparam name="TConfig">The type implementing <see cref="IRaftClusterConfigurer"/>.</typeparam>
        /// <param name="services">A collection of services provided by DI container.</param>
        /// <returns>A collection of services provided by DI container.</returns>
        public static IServiceCollection ConfigureCluster<TConfig>(this IServiceCollection services)
            where TConfig : class, IRaftClusterConfigurer
            => services.AddSingleton<IRaftClusterConfigurer, TConfig>();

        public static IApplicationBuilder UseConsensusProtocolHandler(this IApplicationBuilder builder)
            => builder.Use(RaftProtocolMiddleware.Create);
    }
}
