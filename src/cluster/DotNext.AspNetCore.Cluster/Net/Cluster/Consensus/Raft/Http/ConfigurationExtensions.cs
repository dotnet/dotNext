using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Extensions;
using Messaging;
using Net.Http;
using Replication;

/// <summary>
/// Provides a set of methods for enabling Raft support in ASP.NET Core HTTP application.
/// </summary>
public static partial class ConfigurationExtensions
{
    private static IServiceCollection AddClusterAsSingleton(this IServiceCollection services)
    {
        Func<IServiceProvider, RaftHttpCluster> clusterNodeCast = ServiceProviderServiceExtensions.GetRequiredService<RaftHttpCluster>;

        return services.AddSingleton<RaftHttpCluster>()
            .AddHostedService(clusterNodeCast)
            .AddSingleton<ICluster>(clusterNodeCast)
            .AddSingleton<IRaftHttpCluster>(clusterNodeCast)
            .AddSingleton<IStandbyModeSupport>(clusterNodeCast)
            .AddSingleton<IRaftCluster>(clusterNodeCast)
            .AddSingleton<IMessageBus>(clusterNodeCast)
            .AddSingleton<IReplicationCluster>(clusterNodeCast)
            .AddSingleton<IReplicationCluster<IRaftLogEntry>>(clusterNodeCast)
            .AddSingleton<IPeerMesh<ISubscriber>>(clusterNodeCast)
            .AddSingleton<IPeerMesh<IClusterMember>>(clusterNodeCast)
            .AddSingleton<IPeerMesh<IRaftClusterMember>>(clusterNodeCast);
    }

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh"/>
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
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static IServiceCollection ConfigureLocalNode(this IServiceCollection services, IConfiguration memberConfig)
    {
        Func<IServiceProvider, IOptions<ClusterMemberConfiguration>> configCast = ServiceProviderServiceExtensions.GetRequiredService<IOptions<HttpClusterMemberConfiguration>>;
        return services.Configure<HttpClusterMemberConfiguration>(memberConfig).AddSingleton(configCast).AddClusterAsSingleton();
    }

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
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
    public static IServiceCollection ConfigureLocalNode(this IServiceCollection services, Action<HttpClusterMemberConfiguration> memberConfig)
    {
        Func<IServiceProvider, IOptions<ClusterMemberConfiguration>> configCast = ServiceProviderServiceExtensions.GetRequiredService<IOptions<HttpClusterMemberConfiguration>>;
        return services.Configure(memberConfig).AddSingleton(configCast).AddClusterAsSingleton();
    }

    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    private static void JoinCluster(HostBuilderContext context, IServiceCollection services)
        => ConfigureLocalNode(services, context.Configuration);

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <remarks>
    /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
    /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults(IHostBuilder, Action{IWebHostBuilder})"/>.
    /// </remarks>
    /// <param name="builder">The host builder.</param>
    /// <returns>The modified host builder.</returns>
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static IHostBuilder JoinCluster(this IHostBuilder builder)
        => builder.ConfigureServices(JoinCluster);

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <seealso cref="JoinCluster(IHostBuilder)"></seealso>
    [CLSCompliant(false)]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static void JoinCluster(this WebApplicationBuilder builder)
        => builder.Host.JoinCluster();

    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    private static void JoinCluster(this Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig, HostBuilderContext context, IServiceCollection services)
        => ConfigureLocalNode(services, memberConfig(context.Configuration, context.HostingEnvironment));

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <remarks>
    /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
    /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults(IHostBuilder, Action{IWebHostBuilder})"/>.
    /// </remarks>
    /// <param name="builder">The host builder.</param>
    /// <param name="memberConfig">The delegate that allows to resolve location of local member configuration.</param>
    /// <returns>The modified host builder.</returns>
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static IHostBuilder JoinCluster(this IHostBuilder builder, Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig)
        => builder.ConfigureServices(memberConfig.JoinCluster);

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="memberConfig">The delegate that allows to resolve location of local member configuration.</param>
    /// <seealso cref="JoinCluster(IHostBuilder, Func{IConfiguration, IHostEnvironment, IConfiguration})"/>
    [CLSCompliant(false)]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static void JoinCluster(this WebApplicationBuilder builder, Func<IConfiguration, IHostEnvironment, IConfiguration> memberConfig)
        => builder.Host.JoinCluster(memberConfig);

    private static void JoinCluster(this Action<HttpClusterMemberConfiguration, IConfiguration, IHostEnvironment> memberConfig, HostBuilderContext context, IServiceCollection services)
    {
        var configuration = context.Configuration;
        var environment = context.HostingEnvironment;

        services.ConfigureLocalNode(c => memberConfig(c, configuration, environment));
    }

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <remarks>
    /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
    /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults(IHostBuilder, Action{IWebHostBuilder})"/>.
    /// </remarks>
    /// <param name="builder">The host builder.</param>
    /// <param name="memberConfig">The delegate that allows to resolve location of local member configuration.</param>
    /// <returns>The modified host builder.</returns>
    public static IHostBuilder JoinCluster(this IHostBuilder builder, Action<HttpClusterMemberConfiguration, IConfiguration, IHostEnvironment> memberConfig)
        => builder.ConfigureServices(memberConfig.JoinCluster);

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="memberConfig">The delegate that allows to resolve location of local member configuration.</param>
    /// <seealso cref="JoinCluster(IHostBuilder, Action{HttpClusterMemberConfiguration, IConfiguration, IHostEnvironment})"/>
    [CLSCompliant(false)]
    public static void JoinCluster(this WebApplicationBuilder builder, Action<HttpClusterMemberConfiguration, IConfiguration, IHostEnvironment> memberConfig)
        => builder.Host.JoinCluster(memberConfig);

    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    private static void JoinCluster(this string memberConfigSection, HostBuilderContext context, IServiceCollection services)
        => ConfigureLocalNode(services, context.Configuration.GetSection(memberConfigSection));

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <remarks>
    /// Should be called exactly after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
    /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults(IHostBuilder, Action{IWebHostBuilder})"/>.
    /// </remarks>
    /// <param name="builder">The host builder.</param>
    /// <param name="memberConfigSection">The name of local member configuration section.</param>
    /// <returns>The modified host builder.</returns>
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static IHostBuilder JoinCluster(this IHostBuilder builder, string memberConfigSection)
        => builder.ConfigureServices(memberConfigSection.JoinCluster);

    /// <summary>
    /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IPeerMesh{TPeer}"/>
    /// to application services and establishes network communication with other cluster members.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="memberConfigSection">The name of local member configuration section.</param>
    /// <seealso cref="JoinCluster(IHostBuilder, string)"/>
    [CLSCompliant(false)]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    public static void JoinCluster(this WebApplicationBuilder builder, string memberConfigSection)
        => builder.Host.JoinCluster(memberConfigSection);

    private static void ConfigureConsensusProtocolHandler(this RaftHttpCluster cluster, IApplicationBuilder builder)
        => builder.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = HttpUtils.WriteExceptionContent }).Run(cluster.ProcessRequest);

    /// <summary>
    /// Setup Raft protocol handler as middleware for the specified application.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The configured application builder.</returns>
    [CLSCompliant(false)]
    public static IApplicationBuilder UseConsensusProtocolHandler(this IApplicationBuilder builder)
    {
        var cluster = builder.ApplicationServices.GetRequiredService<RaftHttpCluster>();
        return builder.Map(cluster.ProtocolPath, cluster.ConfigureConsensusProtocolHandler);
    }
}