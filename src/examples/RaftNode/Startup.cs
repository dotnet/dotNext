using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.Threading.Tasks;

namespace RaftNode
{
    internal sealed class Startup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration) => this.configuration = configuration;

        private static Task RedirectToLeader(HttpContext context)
        {
            var cluster = context.RequestServices.GetRequiredService<IRaftCluster>();
            return context.Response.WriteAsync($"Leader address is {cluster.Leader?.EndPoint}. Current address is {context.Connection.LocalIpAddress}:{context.Connection.LocalPort}", context.RequestAborted);
        }

        public void Configure(IApplicationBuilder app)
        {
            const string LeaderResource = "/leader";

            app.UseConsensusProtocolHandler()
                .RedirectToLeader(LeaderResource)
                .UseRouting()
                .UseEndpoints(static endpoints =>
                {
                    endpoints.MapGet(LeaderResource, RedirectToLeader);
                });
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureCluster<ClusterConfigurator>()
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddOptions()
                .AddRouting();

            var path = configuration[SimplePersistentState.LogLocation];
            if (!string.IsNullOrWhiteSpace(path))
            {
                services.AddSingleton<AppEventSource>();
                services.UsePersistenceEngine<IValueProvider, SimplePersistentState>()
                    .AddSingleton<IHostedService, DataModifier>();
            }
        }
    }
}