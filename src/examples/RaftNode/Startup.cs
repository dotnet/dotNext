using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
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
            app.UseConsensusProtocolHandler();
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IRaftClusterConfigurator, ClusterConfigurator>();
            services.AddOptions().BecomeClusterMember(configuration);
        }
    }
}