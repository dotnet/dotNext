using System.Net.Http;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using DotNext.Net.Cluster.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RaftNode
{
    internal sealed class Startup : StartupBase
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration) => this.configuration = configuration;

        public override void Configure(IApplicationBuilder app)
        {
            app.UseConsensusProtocolHandler().ApplicationServices.GetService<FileListener>();
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IRaftClusterConfigurator, ClusterConfigurator>()
                .AddSingleton<FileListener>()
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddOptions().BecomeClusterMember(configuration);
        }
    }
}