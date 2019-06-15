using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal static class RaftHttpConfigurator
    {
        internal static IServiceCollection AddClusterAsSingleton<TCluster>(this IServiceCollection services, IConfiguration memberConfig)
            where TCluster : RaftHttpCluster
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<RaftHttpCluster>;
            return services.Configure<ClusterMemberConfiguration>(memberConfig)
                .AddSingleton<RaftHttpCluster, TCluster>()
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IRaftCluster>(clusterNodeCast)
                .AddSingleton<IExpandableCluster>(clusterNodeCast);
        }
    }
}
