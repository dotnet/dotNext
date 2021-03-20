using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

namespace RaftNode
{
    internal sealed class Startup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration) => this.configuration = configuration;

        public void Configure(IApplicationBuilder app)
        {
            app.UseConsensusProtocolHandler();
        }

        private static void ConfigureBuffering(RaftLogEntryBufferingOptions options)
        {
            options.MemoryThreshold = 512;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureCluster<ClusterConfigurator>()
                .EnableBuffering(ConfigureBuffering)
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddOptions();
            var path = configuration[SimplePersistentState.LogLocation];
            if (!string.IsNullOrWhiteSpace(path))
            {
                services.UsePersistenceEngine<IValueProvider, SimplePersistentState>()
                    .AddSingleton<IHostedService, DataModifier>();
            }
        }
    }
}