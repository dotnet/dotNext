using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http
{
    using Net.Http;

    /// <summary>
    /// Represents configuration methods that allows to embed HyParView membership
    /// protocol into ASP.NET Core application.
    /// </summary>
    [CLSCompliant(false)]
    public static class ConfigurationExtensions
    {
        private static IServiceCollection AddPeerController(this IServiceCollection services)
        {
            Func<IServiceProvider, HttpPeerController> controllerCast = ServiceProviderServiceExtensions.GetRequiredService<HttpPeerController>;
            return services.AddSingleton<HttpPeerController>()
                .AddSingleton<PeerController>(controllerCast)
                .AddSingleton<IPeerMesh<HttpPeerClient>>(controllerCast);
        }

        /// <summary>
        /// Allows to inject <see cref="PeerController"/>, <see cref="IPeerMesh{TPeer}"/>
        /// to application services and establishes network communication with overlay.
        /// </summary>
        /// <param name="services">The collection of services.</param>
        /// <param name="configuration">The configuration of local peer.</param>
        /// <returns>The modified collection of services.</returns>
        public static IServiceCollection ConfigureLocalPeer(this IServiceCollection services, IConfiguration configuration)
        {
            Func<IServiceProvider, IOptions<PeerConfiguration>> configCast = ServiceProviderServiceExtensions.GetRequiredService<IOptions<HttpPeerConfiguration>>;
            return services.Configure<HttpPeerConfiguration>(configuration).AddSingleton(configCast).AddPeerController();
        }

        /// <summary>
        /// Allows to inject <see cref="PeerController"/>, <see cref="IPeerMesh{TPeer}"/>
        /// to application services and establishes network communication with overlay.
        /// </summary>
        /// <param name="services">The collection of services.</param>
        /// <param name="configuration">The delegate that can be used to configure the local peer.</param>
        /// <returns>The modified collection of services.</returns>
        public static IServiceCollection ConfigureLocalPeer(this IServiceCollection services, Action<HttpPeerConfiguration> configuration)
        {
            Func<IServiceProvider, IOptions<PeerConfiguration>> configCast = ServiceProviderServiceExtensions.GetRequiredService<IOptions<HttpPeerConfiguration>>;
            return services.Configure<HttpPeerConfiguration>(configuration).AddSingleton(configCast).AddPeerController();
        }

        private static void JoinMesh(HostBuilderContext context, IServiceCollection services)
            => services.ConfigureLocalPeer(context.Configuration);

        /// <summary>
        /// Allows to inject <see cref="PeerController"/>, <see cref="IPeerMesh{TPeer}"/>
        /// to application services and establishes network communication with overlay.
        /// </summary>
        /// <remarks>
        /// Should be called immediately after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
        /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults"/>.
        /// </remarks>
        /// <param name="builder">The host builder.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinMesh(this IHostBuilder builder)
            => builder.ConfigureServices(JoinMesh);

        private static void JoinMesh(this Func<IConfiguration, IHostEnvironment, IConfiguration> peerConfig, HostBuilderContext context, IServiceCollection services)
            => services.ConfigureLocalPeer(peerConfig(context.Configuration, context.HostingEnvironment));

        /// <summary>
        /// Allows to inject <see cref="PeerController"/>, <see cref="IPeerMesh{TPeer}"/>
        /// to application services and establishes network communication with overlay.
        /// </summary>
        /// <remarks>
        /// Should be called immediately after <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
        /// or <see cref="GenericHostBuilderExtensions.ConfigureWebHostDefaults"/>.
        /// </remarks>
        /// <param name="builder">The host builder.</param>
        /// <param name="peerConfig">The delegate that can be used to provide local peer configuration.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinMesh(this IHostBuilder builder, Func<IConfiguration, IHostEnvironment, IConfiguration> peerConfig)
            => builder.ConfigureServices(peerConfig.JoinMesh);

        private static void JoinMesh(this Action<HttpPeerConfiguration, IConfiguration, IHostEnvironment> peerConfig, HostBuilderContext context, IServiceCollection services)
        {
            var configuration = context.Configuration;
            var environment = context.HostingEnvironment;

            services.ConfigureLocalPeer(c => peerConfig(c, configuration, environment));
        }

        /// <summary>
        /// Allows to inject <see cref="PeerController"/>, <see cref="IPeerMesh{TPeer}"/>
        /// to application services and establishes network communication with overlay.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <param name="peerConfig">The delegate that can be used to provide local peer configuration.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinMesh(this IHostBuilder builder, Action<HttpPeerConfiguration, IConfiguration, IHostEnvironment> peerConfig)
            => builder.ConfigureServices(peerConfig.JoinMesh);

        private static void JoinMesh(this string configSection, HostBuilderContext context, IServiceCollection services)
            => services.ConfigureLocalPeer(context.Configuration.GetSection(configSection));

        /// <summary>
        /// Allows to inject <see cref="PeerController"/>, <see cref="IPeerMesh{TPeer}"/>
        /// to application services and establishes network communication with overlay.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <param name="configSection">The name of configuration section containing configuration of the local peer.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder JoinMesh(this IHostBuilder builder, string configSection)
            => builder.ConfigureServices(configSection.JoinMesh);

        private static void ConfigureHyParViewProtocolHandler(this HttpPeerController controller, IApplicationBuilder builder)
            => builder.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = HttpUtils.WriteExceptionContent }).Run(controller.ProcessRequest);

        /// <summary>
        /// Setup HyParView protocol handler as a middleware for the specified application.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The modified application builder.</returns>
        public static IApplicationBuilder UseHyParViewProtocolHandler(this IApplicationBuilder builder)
        {
            var controller = builder.ApplicationServices.GetRequiredService<HttpPeerController>();
            return builder.Map(controller.ResourcePath, controller.ConfigureHyParViewProtocolHandler);
        }
    }
}