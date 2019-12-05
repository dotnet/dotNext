using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    /// <summary>
    /// Configure ASP.NET Core application to use
    /// dedicated web host and separated port for Raft endpoint.
    /// </summary>
    /// <seealso cref="IDedicatedHostBuilder"/>
    [CLSCompliant(false)]
    public static class ConfigurationExtensions
    {
        private static void ConfigureClusterMember(HostBuilderContext context, IServiceCollection services)
            => services.AddClusterAsSingleton<RaftHostedCluster, RaftHostedClusterMemberConfiguration>(context.Configuration);

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <param name="builder">The builder of main application host.</param>
        /// <returns>The builder of main application host.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder)
            => builder.ConfigureServices(ConfigureClusterMember);
    }
}
