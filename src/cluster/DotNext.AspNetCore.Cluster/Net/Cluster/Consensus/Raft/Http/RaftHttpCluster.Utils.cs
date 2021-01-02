using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Replication;

    internal partial class RaftHttpCluster
    {
        internal static IServiceCollection AddClusterAsSingleton<TCluster, TConfig>(IServiceCollection services, IConfiguration memberConfig)
            where TCluster : RaftHttpCluster
            where TConfig : HttpClusterMemberConfiguration, new()
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<TCluster>;
            return services.Configure<TConfig>(memberConfig)
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