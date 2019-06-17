using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal static class RaftHttpConfigurator
    {
        internal static IServiceCollection AddClusterAsSingleton<TCluster, TConfig>(this IServiceCollection services, IConfiguration memberConfig)
            where TCluster : RaftHttpCluster
            where TConfig : RaftClusterMemberConfiguration, new()
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<TCluster>;
            return services.Configure<TConfig>(memberConfig)
                .Configure<RaftClusterMemberConfiguration>(memberConfig)
                .AddSingleton<TCluster>()
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IRaftCluster>(clusterNodeCast)
                .AddSingleton<IExpandableCluster>(clusterNodeCast);
        }
    }
}
