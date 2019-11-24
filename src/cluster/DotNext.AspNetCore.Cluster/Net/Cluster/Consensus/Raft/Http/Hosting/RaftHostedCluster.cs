using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedCluster : RaftHttpCluster
    {
        private static readonly Uri Root = new Uri("/", UriKind.Relative);
        private readonly IHost host;

        public RaftHostedCluster(IServiceProvider services)
            : base(services, out var members)
        {
            var config = services.GetRequiredService<IOptions<RaftHostedClusterMemberConfiguration>>().Value;
            var hostConfigurer = services.GetService<ClusterMemberHostBuilder>();
            host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    hostConfigurer.Configure(webHost, config);
                    webHost.Configure(app =>
                    {
                        hostConfigurer.Configure(app);
                        app
                            .UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = RaftHttpConfigurator.WriteExceptionContent })
                            .Run(ProcessRequest);
                    });
                })
                .Build();
            config.SetupHostAddressHint(host.Services.GetRequiredService<IServer>().Features);
            foreach (var memberUri in config.Members)
                members.Add(CreateMember(memberUri));
        }

        private protected override RaftClusterMember CreateMember(Uri address)
        {
            var member = new RaftClusterMember(this, address, Root);
            ConfigureMember(member);
            return member;
        }

        public override async Task StartAsync(CancellationToken token)
        {
            await host.StartAsync(token).ConfigureAwait(false);
            await base.StartAsync(token).ConfigureAwait(false);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await host.StopAsync(token).ConfigureAwait(false);
            await base.StopAsync(token).ConfigureAwait(false);
        }

        private protected override Predicate<RaftClusterMember> LocalMemberFinder => host.Services.GetRequiredService<IServer>().GetHostingAddresses().Contains;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                host.Dispose();
            base.Dispose(disposing);
        }
    }
}
