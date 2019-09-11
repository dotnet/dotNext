using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Replication;

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
                .AddSingleton(clusterNodeCast)
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IRaftCluster>(clusterNodeCast)
                .AddSingleton<IMessageBus>(clusterNodeCast)
                .AddSingleton<IReplicationCluster>(clusterNodeCast)
                .AddSingleton<IReplicationCluster<IRaftLogEntry>>(clusterNodeCast)
                .AddSingleton<IExpandableCluster>(clusterNodeCast);
        }

        internal static Task WriteExceptionContent(HttpContext context)
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            return feature is null ? Task.CompletedTask : context.Response.WriteAsync(feature.Error.ToString());
        }
    }
}
