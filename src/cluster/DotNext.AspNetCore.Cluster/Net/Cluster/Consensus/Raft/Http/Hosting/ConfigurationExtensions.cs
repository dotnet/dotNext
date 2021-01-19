﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <remarks>
        /// This method should not be used together with <see cref="JoinCluster(IHostBuilder)"/>
        /// because it has the same semantics. It's here just for corner case when you
        /// want to implement choice between hosted and embedded mode in the same app or library.
        /// </remarks>
        /// <param name="services">The collection of services.</param>
        /// <param name="memberConfig">The configuration of local cluster node.</param>
        /// <returns>The modified collection of services.</returns>
        public static IServiceCollection ConfigureLocalNode(this IServiceCollection services, IConfiguration memberConfig)
            => RaftHttpCluster.AddClusterAsSingleton<RaftHostedCluster, RaftHostedClusterMemberConfiguration>(services, memberConfig);

        private static void ConfigureClusterMember(HostBuilderContext context, IServiceCollection services)
            => ConfigureLocalNode(services, context.Configuration);

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <param name="builder">The builder of main application host.</param>
        /// <returns>The builder of the application host.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder)
            => builder.ConfigureServices(ConfigureClusterMember);

        private static void JoinCluster(this Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig, HostBuilderContext context, IServiceCollection services)
            => ConfigureLocalNode(services, memberConfig(context.Configuration, context.HostingEnvironment));

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <param name="builder">The builder of main application host.</param>
        /// <param name="memberConfig">The delegate that allows to resolve location of local member configuration.</param>
        /// <returns>The builder of the application host.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder, Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig)
            => builder.ConfigureServices(memberConfig.JoinCluster);

        private static void JoinCluster(this string memberConfigSection, HostBuilderContext context, IServiceCollection services)
            => ConfigureLocalNode(services, context.Configuration.GetSection(memberConfigSection));

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <param name="builder">The builder of main application host.</param>
        /// <param name="memberConfigSection">The name of local member configuration section.</param>
        /// <returns>The builder of the application host.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder, string memberConfigSection)
            => builder.ConfigureServices(memberConfigSection.JoinCluster);
    }
}
