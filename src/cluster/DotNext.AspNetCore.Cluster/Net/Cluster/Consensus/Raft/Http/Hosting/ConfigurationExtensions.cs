using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using static System.Globalization.CultureInfo;
using DefaultWebHostBuilder = Microsoft.AspNetCore.Hosting.WebHostBuilder;

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
        /// The name of the member configuration property
        /// allows to specify hosting port for the consensus protocol
        /// handler.
        /// </summary>
        public const string HostPortConfigurationOption = "port";

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
            if (hostBuilder is null)
            {
                hostBuilder = new DefaultWebHostBuilder();
                var port = int.Parse(memberConfig[HostPortConfigurationOption], InvariantCulture);
                hostBuilder.UseKestrel(options => options.ListenAnyIP(port));
            }

            return services.AddSingleton(new WebHostBuilder(hostBuilder))
                .AddClusterAsSingleton<RaftHostedCluster>(memberConfig);
        }
    }
}
