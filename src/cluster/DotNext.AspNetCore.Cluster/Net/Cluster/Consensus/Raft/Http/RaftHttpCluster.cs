using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
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

    internal sealed class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, ISite, IRaftCluster, IExpandableCluster, ILocalClusterMember
    {

        private readonly IRaftClusterConfigurer configurer;
        private readonly IMessageHandler messageHandler;
        private readonly IReplicator replicator;

        private readonly Guid id;
        private readonly IDisposable configurationTracker;
        private volatile MemberMetadata metadata;
        private volatile ISet<IPNetwork> allowedNetworks;
        private readonly Uri consensusResource;

        private RaftHttpCluster(RaftClusterMemberConfiguration config)
            : base(config)
        {
            consensusResource = config.ResourcePath;
            id = Guid.NewGuid();
            allowedNetworks = config.ParseAllowedNetworks();
            SetMembers(config.Members, CreateMember);
            metadata = new MemberMetadata(config.Metadata);
        }

        private RaftHttpCluster(IOptionsMonitor<RaftClusterMemberConfiguration> config, IServiceProvider dependencies)
            : this(config.CurrentValue)
        {
            configurer = dependencies.GetService<IRaftClusterConfigurer>();
            messageHandler = dependencies.GetService<IMessageHandler>();
            replicator = dependencies.GetService<IReplicator>();
            //track changes in configuration
            configurationTracker = config.OnChange(ConfigurationChanged);
        }

        public RaftHttpCluster(IServiceProvider dependencies)
            : this(dependencies.GetRequiredService<IOptionsMonitor<RaftClusterMemberConfiguration>>(), dependencies)
        {
            
        }

        private RaftClusterMember CreateMember(Uri address) => new RaftClusterMember(this, address, consensusResource);

        private void ConfigurationChanged(RaftClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.ParseAllowedNetworks();
            //detect new members
            
            //detect deleted members
        }

        ref readonly Guid ILocalClusterMember.Id => ref id;

        IReadOnlyDictionary<string, string> ILocalClusterMember.Metadata => metadata;

        ILocalClusterMember ISite.LocalMember => this;

        bool ISite.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        public event ClusterChangedEventHandler MemberAdded;
        public event ClusterChangedEventHandler MemberRemoved;
        public override event ClusterMemberStatusChanged MemberStatusChanged;

        void ISite.MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            => MemberStatusChanged?.Invoke(member, previousStatus, newStatus);

        private Task Vote(RequestVoteMessage request, HttpResponse response)
            => RequestVoteMessage.CreateResponse(response, this, request.MemberId == id || Vote(request.ConsensusTerm));

        private Task Resign(HttpResponse response) => ResignMessage.CreateResponse(response, this, Resign());

        private Task GetMetadata(HttpResponse response) => MetadataMessage.CreateResponse(response, this, metadata);

        private async Task ReceiveAppendEntries(RaftHttpMessage request, HttpResponse response)
        {
            if(request.MemberId == id)  //sender node and receiver are same, ignore message
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

        internal Task ProcessRequest(HttpContext context)
        {
            var networks = allowedNetworks;
            //checks whether the client's address is allowed
            if(networks.Count > 0 || networks.FirstOrDefault(context.Connection.RemoteIpAddress.IsIn) is null)
            {
                context.Response.StatusCode = (int) HttpStatusCode.Forbidden;
                return Task.CompletedTask;
            }
            //process request
            switch (RaftHttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return Vote(new RequestVoteMessage(context.Request),  context.Response);
                case ResignMessage.MessageType:
                    return Resign(context.Response);
                case MetadataMessage.MessageType:
                    return GetMetadata(context.Response);
                default:
                    context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                    return Task.CompletedTask;
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
