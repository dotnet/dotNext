using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
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

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and enables network communications necessary to serve cluster member.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="memberConfig">The configuration of cluster member.</param>
        /// <returns>The collection of injectable services.</returns>
        public static IServiceCollection EnableClusterSupport(this IServiceCollection services, IConfiguration memberConfig)
            => services.Configure<ClusterMemberConfiguration>(memberConfig).AddSingleton<RaftHttpCluster>().EnableCluster();

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

        /// <summary>
        /// Setup Raft protocol handler as middleware for the specified application.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The configured application builder.</returns>
        public static IApplicationBuilder BecomeClusterMember(this IApplicationBuilder builder)
            => builder.Use(RaftProtocolMiddleware.Create);
    }
}
