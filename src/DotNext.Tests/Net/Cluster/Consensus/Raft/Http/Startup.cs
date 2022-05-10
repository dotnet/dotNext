using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    [ExcludeFromCodeCoverage]
    internal sealed class Startup
    {
        internal const string PersistentConfigurationPath = "persistentConfigPath";

        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration) => this.configuration = configuration;

        public void Configure(IApplicationBuilder app)
        {
            app.UseConsensusProtocolHandler();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions()
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddSingleton<IInputChannel, TestMessageHandler>()
                .AddSingleton<IInputChannel, Mailbox>()
                .AddSingleton<MetricsCollector, TestMetricsCollector>();

            var configPath = configuration[PersistentConfigurationPath];

            if (configPath is { Length: > 0 })
                services.UsePersistentConfigurationStorage(configPath);
        }
    }
}
