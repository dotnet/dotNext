using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Replication;

    internal sealed class RaftHttpCluster : RaftCluster<RaftClusterMember>, IMiddleware, IHostedService, ISite, IRaftCluster, IExpandableCluster
    {
        

        private readonly IRaftClusterConfigurer configurer;
        private readonly IMessageHandler messageHandler;
        private readonly IReplicator replicator;

        private readonly Guid id;
        private readonly IDisposable configurationTracker;
        private volatile Dictionary<string, string> metadata;

        private RaftHttpCluster(IOptionsMonitor<RaftClusterMemberConfiguration> config, IServiceProvider dependencies)
            : base(config.CurrentValue)
        {
            id = Guid.NewGuid();
            configurer = dependencies.GetService<IRaftClusterConfigurer>();
            messageHandler = dependencies.GetService<IMessageHandler>();
            replicator = dependencies.GetService<IReplicator>();
            metadata = config.CurrentValue.Metadata;
            //track changes in configuration
            configurationTracker = config.OnChange(ConfigurationChanged);
            //create members
            SetMembers<Uri>(config.CurrentValue.Members, CreateMember);
        }

        private RaftClusterMember CreateMember(Uri address) => new RaftClusterMember(this, address);

        public RaftHttpCluster(IServiceProvider dependencies)
            : this(dependencies.GetRequiredService<IOptionsMonitor<RaftClusterMemberConfiguration>>(), dependencies)
        {
            
        }

        private void ConfigurationChanged(RaftClusterMemberConfiguration configuration, string name)
        {
            metadata = configuration.Metadata;
        }

        ref readonly Guid ISite.LocalMemberId => ref id;

        bool ISite.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        public override event ClusterMemberStatusChanged MemberStatusChanged;

        void ISite.MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            => MemberStatusChanged?.Invoke(member, previousStatus, newStatus);

        private async Task Vote(RequestVoteMessage request, HttpResponse response)
            => await RequestVoteMessage.CreateResponse(response, this, await Vote(request, request.ConsensusTerm).ConfigureAwait(false))
                .ConfigureAwait(false);

        private async Task Resign(HttpResponse response) =>
            await ResignMessage.CreateResponse(response, this, await Resign().ConfigureAwait(false))
                .ConfigureAwait(false);

        private async Task ReceiveAppendEntries(RaftHttpMessage request, HttpResponse response)
        {
            if(request.MemberId == LocalMemberId)  //sender node and receiver are same, ignore message
                return;
        }

        public override Task StartAsync(CancellationToken token)
        {
            configurer?.Initialize(this);
            return base.StartAsync(token);
        }

        public override Task StopAsync(CancellationToken token)
        {
            configurer?.Cleanup(this);
            return base.StopAsync(token);
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            switch (RaftHttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return Vote(new RequestVoteMessage(context.Request),  context.Response);
                case ResignMessage.MessageType:
                    return Resign(context.Response);
                default:
                    return next(context);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
                configurationTracker.Dispose();
            base.Dispose(disposing);
        }
    }
}
