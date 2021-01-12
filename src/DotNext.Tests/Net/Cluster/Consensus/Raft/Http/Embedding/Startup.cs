using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    using Messaging;

    [ExcludeFromCodeCoverage]
    internal sealed class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseConsensusProtocolHandler();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions()
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddSingleton<IInputChannel, Mailbox>()
                .AddSingleton<MetricsCollector, TestMetricsCollector>();
        }
    }
}
