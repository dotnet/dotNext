using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    /// <summary>
    /// Configure ASP.NET Core application to reuse application's
    /// web host and port for Raft endpoint.
    /// </summary>
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
            => services.AddClusterAsSingleton<RaftEmbeddedCluster, RaftEmbeddedClusterMemberConfiguration>(memberConfig);

        private static void JoinCluster(HostBuilderContext context, IServiceCollection services)
            => ConfigureLocalNode(services, context.Configuration);

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <remarks>
        /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
        /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults"/>.
        /// </remarks>
        /// <param name="builder">The host builder.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder)
            => builder.ConfigureServices(JoinCluster);

        private static void JoinCluster(this Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig, HostBuilderContext context, IServiceCollection services)
            => ConfigureLocalNode(services, memberConfig(context.Configuration, context.HostingEnvironment));

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <remarks>
        /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
        /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults"/>.
        /// </remarks>
        /// <param name="builder">The host builder.</param>
        /// <param name="memberConfig">The delegate that allows to resolve location of local member configuration.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder, Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig)
            => builder.ConfigureServices(memberConfig.JoinCluster);

        private static void JoinCluster(this string memberConfigSection, HostBuilderContext context, IServiceCollection services)
            => ConfigureLocalNode(services, context.Configuration.GetSection(memberConfigSection));

        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster members.
        /// </summary>
        /// <remarks>
        /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
        /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults"/>.
        /// </remarks>
        /// <param name="builder">The host builder.</param>
        /// <param name="memberConfigSection">The name of local member configuration section.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinCluster(this IHostBuilder builder, string memberConfigSection)
            => builder.ConfigureServices(memberConfigSection.JoinCluster);

        private static void ConfigureConsensusProtocolHandler(this RaftHttpCluster cluster, IApplicationBuilder builder)
            => builder.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = RaftHttpConfiguration.WriteExceptionContent }).Run(cluster.ProcessRequest);

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
