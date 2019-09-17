using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;

namespace RaftNode
{
    internal sealed class Startup : StartupBase
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration) => this.configuration = configuration;

        public override void Configure(IApplicationBuilder app)
        {
            app.UseConsensusProtocolHandler();
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IRaftClusterConfigurator, ClusterConfigurator>()
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddOptions()
                .BecomeClusterMember(configuration);
            var path = configuration[SimplePersistentState.LogLocation];
            if (!string.IsNullOrWhiteSpace(path))
            {
                Func<IServiceProvider, SimplePersistentState> serviceCast = ServiceProviderServiceExtensions.GetRequiredService<SimplePersistentState>;
                services
                    .AddSingleton<SimplePersistentState>()
                    .AddSingleton<IPersistentState>(serviceCast)
                    .AddSingleton<IValueProvider>(serviceCast)
                    .AddSingleton<IHostedService, DataModifier>();
            }
        }
    }
}