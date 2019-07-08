using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedCluster : RaftHttpCluster
    {
        private static readonly Uri Root = new Uri("/", UriKind.Relative);
        private readonly IWebHost host;

        public RaftHostedCluster(IServiceProvider services)
            : base(services, out var members)
        {
            var config = services.GetRequiredService<IOptions<RaftHostedClusterMemberConfiguration>>().Value;
            var appConfigurer = services.GetService<ApplicationBuilder>();
            host = services.GetRequiredService<WebHostBuilder>()
                .Configure(config)
                .Configure(app => 
                {
                    appConfigurer?.Invoke(app);
                    app
                        .UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = RaftHttpConfigurator.WriteExceptionContent })
                        .Run(ProcessRequest);
                })
                .Build();
            foreach (var memberUri in config.Members)
                members.Add(CreateMember(memberUri));
        }

        private protected override RaftClusterMember CreateMember(Uri address)
        {
            var member = new RaftClusterMember(this, address, Root) { Timeout = RequestTimeout };
            member.DefaultRequestHeaders.ConnectionClose = OpenConnectionForEachRequest;
            return member;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                host.Dispose();
            base.Dispose(disposing);
        }
    }
}
