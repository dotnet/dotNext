using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        private static void ConfigureClusterMember(Func<IConfiguration, IClusterMemberHostBuilder>? builderFactory, IConfiguration memberConfig, IServiceCollection services)
            => services
                  .AddSingleton(new ClusterMemberHostBuilder(builderFactory?.Invoke(memberConfig)))
                    .AddClusterAsSingleton<RaftHostedCluster, RaftHostedClusterMemberConfiguration>(memberConfig);

        private static void ConfigureClusterMember(this Func<IConfiguration, IClusterMemberHostBuilder>? builderFactory, HostBuilderContext context, IServiceCollection services)
            => ConfigureClusterMember(builderFactory, context.Configuration, services);

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <param name="builder">The builder of main application host.</param>
        /// <param name="memberHostFactory">The factory of the dedicated host.</param>
        /// <returns>The builder of main application host.</returns>
        public static IHostBuilder BecomeClusterMember(this IHostBuilder builder, Func<IConfiguration, IClusterMemberHostBuilder>? memberHostFactory = null)
            => builder.ConfigureServices(memberHostFactory.ConfigureClusterMember);
    }
}
