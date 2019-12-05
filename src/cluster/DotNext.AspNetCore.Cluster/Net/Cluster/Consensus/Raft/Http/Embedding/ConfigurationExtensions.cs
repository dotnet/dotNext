using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    /// <summary>
    /// Configure ASP.NET Core application to reuse application's
    /// web host and port for Raft endpoint.
    /// </summary>
    [CLSCompliant(false)]
    public static class ConfigurationExtensions
    {
        private static void JoinCluster(HostBuilderContext context, IServiceCollection services)
            => services.AddClusterAsSingleton<RaftEmbeddedCluster, RaftEmbeddedClusterMemberConfiguration>(context.Configuration);

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <remarks>
        /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost"/>
        /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults"/>.
        /// </remarks>
        /// <param name="builder">The host builder.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder)
            => builder.ConfigureServices(JoinCluster);

        private static void ConfigureConsensusProtocolHandler(this RaftHttpCluster cluster, IApplicationBuilder builder)
            => builder.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = RaftHttpConfigurator.WriteExceptionContent }).Run(cluster.ProcessRequest);

        /// <summary>
        /// Setup Raft protocol handler as middleware for the specified application.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The configured application builder.</returns>
        public static IApplicationBuilder UseConsensusProtocolHandler(this IApplicationBuilder builder)
        {
            var cluster = builder.ApplicationServices.GetRequiredService<RaftEmbeddedCluster>();
            return builder.Map(cluster.ProtocolPath, cluster.ConfigureConsensusProtocolHandler);
        }
    }
}
