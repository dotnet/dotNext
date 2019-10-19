namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    using Messaging;

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
            services.AddOptions()
                .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                .AddSingleton<IMessageHandler, Mailbox>()
                .AddSingleton<MetricsCollector, TestMetricsCollector>()
                .BecomeClusterMember(configuration);
        }
    }
}
