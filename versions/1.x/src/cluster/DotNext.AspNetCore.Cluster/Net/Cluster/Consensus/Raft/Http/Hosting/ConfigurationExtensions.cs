using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    /// <summary>
    /// Allows to configure Raft-related stuff and turns
    /// the web application into cluster member.
    /// </summary>
    /// <remarks>
    /// Raft-related endpoint handler is hosted on dedicated port and
    /// separated from existing application.
    /// </remarks>
    [CLSCompliant(false)]
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster member.
        /// </summary>
        /// <param name="services">The registry of services.</param>
        /// <param name="memberConfig">The configuration of cluster member.</param>
        /// <param name="hostBuilder">The builder of the host for the consensus protocol handler. May be <see langword="null"/> to use Kestrel-based host.</param>
        /// <param name="appBuilder">The builder of consensus protocol processing pipeline. May be <see langword="null"/>.</param>
        /// <returns>The collection of injectable services.</returns>
        public static IServiceCollection BecomeClusterMember(this IServiceCollection services,
            IConfiguration memberConfig, IWebHostBuilder hostBuilder = null, ApplicationBuilder appBuilder = null)
        {
            if (appBuilder != null)
                services = services.AddSingleton(appBuilder);
            return services.AddSingleton(hostBuilder is null ? new WebHostBuilder() : new WebHostBuilder(hostBuilder))
                .AddClusterAsSingleton<RaftHostedCluster, RaftHostedClusterMemberConfiguration>(memberConfig);
        }
    }
}
