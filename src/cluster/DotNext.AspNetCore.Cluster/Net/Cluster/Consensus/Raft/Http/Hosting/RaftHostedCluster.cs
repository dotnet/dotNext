using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedCluster : RaftHttpCluster
    {
        private readonly IWebHost host;

        public RaftHostedCluster(IServiceProvider services)
            : base(services)
        {
            var appConfigurer = services.GetService<ApplicationBuilder>();
            host = services.GetRequiredService<WebHostBuilder>()
                .Configure(app => (appConfigurer?.Invoke(app) ?? app).Run(ProcessRequest))
                .Build();
        }

        public override async Task StartAsync(CancellationToken token)
        {
            await base.StartAsync(token).ConfigureAwait(false);
            await host.StartAsync(token).ConfigureAwait(false);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await host.StopAsync(token).ConfigureAwait(false);
            await base.StopAsync(token).ConfigureAwait(false);
        }

        private new async Task ProcessRequest(HttpContext context)
        {
            if (await base.ProcessRequest(context).ConfigureAwait(false))
                return;
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                host.Dispose();
            base.Dispose(disposing);
        }
    }
}
