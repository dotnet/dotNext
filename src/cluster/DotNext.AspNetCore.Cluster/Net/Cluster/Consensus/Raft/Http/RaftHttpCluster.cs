using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Generic;
    using Messaging;
    using Replication;
    using Threading.Tasks;

    internal sealed class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IRaftCluster, IExpandableCluster, ILocalClusterMember
    {
        private delegate ICollection<IPEndPoint> HostingAddressesProvider();

        private readonly IRaftClusterConfigurer configurer;
        private readonly IMessageHandler messageHandler;
        private readonly IReplicator replicator;

        private readonly IDisposable configurationTracker;
        private volatile MemberMetadata metadata;
        private volatile ISet<IPNetwork> allowedNetworks;
        private readonly Uri consensusPath;
        private RaftClusterMember localMember;
        private readonly HostingAddressesProvider hostingAddresses;

        private RaftHttpCluster(RaftClusterMemberConfiguration config)
            : base(config, out var members)
        {
            consensusPath = config.ResourcePath;
            allowedNetworks = config.ParseAllowedNetworks();
            metadata = new MemberMetadata(config.Metadata);
            foreach (var memberUri in config.Members)
                members.Add(CreateMember(memberUri));
        }

        private RaftHttpCluster(IOptionsMonitor<RaftClusterMemberConfiguration> config, IServiceProvider dependencies)
            : this(config.CurrentValue)
        {
            configurer = dependencies.GetService<IRaftClusterConfigurer>();
            messageHandler = dependencies.GetService<IMessageHandler>();
            replicator = dependencies.GetService<IReplicator>();
            hostingAddresses = dependencies.GetRequiredService<IServer>().GetHostingAddresses;
            //track changes in configuration
            configurationTracker = config.OnChange(ConfigurationChanged);
        }

        public RaftHttpCluster(IServiceProvider dependencies)
            : this(dependencies.GetRequiredService<IOptionsMonitor<RaftClusterMemberConfiguration>>(), dependencies)
        {
        }

        private RaftClusterMember CreateMember(Uri address) => new RaftClusterMember(this, address, consensusPath);

        private void ConfigurationChanged(RaftClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.ParseAllowedNetworks();
            ChangeMembers((in MemberCollection members) =>
            {
                var existingMembers = new HashSet<Uri>();
                //remove members
                foreach (var holder in members)
                    if (configuration.Members.Contains(holder.Member.BaseAddress))
                        existingMembers.Add(holder.Member.BaseAddress);
                    else
                    {
                        var member = holder.Remove();
                        MemberRemoved?.Invoke(this, member);
                        member.CancelPendingRequests();
                    }

                //add new members
                foreach (var memberUri in configuration.Members)
                    if (!existingMembers.Contains(memberUri))
                    {
                        var member = CreateMember(memberUri);
                        members.Add(member);
                        MemberAdded?.Invoke(this, member);
                    }
                existingMembers.Clear();
            });
        }

        IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

        bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        IPEndPoint IHostingContext.LocalEndpoint => localMember?.Endpoint;

        public event ClusterChangedEventHandler MemberAdded;
        public event ClusterChangedEventHandler MemberRemoved;
        public override event ClusterMemberStatusChanged MemberStatusChanged;

        void IHostingContext.MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            => MemberStatusChanged?.Invoke(member, previousStatus, newStatus);

        public override Task StartAsync(CancellationToken token)
        {
            //detect local member
            localMember = Members.FirstOrDefault(hostingAddresses().Contains);
            if (localMember is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
            configurer?.Initialize(this);
            return base.StartAsync(token);
        }

        public override Task StopAsync(CancellationToken token)
        {
            configurer?.Cleanup(this);
            return base.StopAsync(token);
        }

        private static bool True(Task task) => true;

        private async Task Vote(RequestVoteMessage request, HttpResponse response)
            => await RequestVoteMessage.CreateResponse(response,
                    await Vote(request.Sender, request.ConsensusTerm, ClusterMember.Represents).ConfigureAwait(false))
                .ConfigureAwait(false);

        private async Task Resign(HttpResponse response) => 
            await ResignMessage.CreateResponse(response, await Resign().ConfigureAwait(false)).ConfigureAwait(false);

        private Task GetMetadata(HttpResponse response) => MetadataMessage.CreateResponse(response, this, metadata);

        private async Task ReceiveEntries(RaftHttpMessage request, HttpResponse response)
        {
            if (request.MemberId == id)  //sender node and receiver are same, ignore message
                return;
            ReceiveEntries()
        }

        internal Task<bool> ProcessRequest(HttpContext context)
        {
            var networks = allowedNetworks;
            if (!string.Equals(consensusPath.GetComponents(UriComponents.Path, UriFormat.UriEscaped),
                context.Request.PathBase.Value, StringComparison.Ordinal))
                return CompletedTask<bool, BooleanConst.False>.Task;
            //checks whether the client's address is allowed
            if (networks.Count > 0 || networks.FirstOrDefault(context.Connection.RemoteIpAddress.IsIn) is null)
            {
                context.Response.StatusCode = (int) HttpStatusCode.Forbidden;
                return CompletedTask<bool, BooleanConst.True>.Task;
            }

            Task task;
            //process request
            switch (RaftHttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    task = Vote(new RequestVoteMessage(context.Request), context.Response);
                    break;
                case ResignMessage.MessageType:
                    task = Resign(context.Response);
                    break;
                case MetadataMessage.MessageType:
                    task = GetMetadata(context.Response);
                    break;
                default:
                    context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                    return CompletedTask<bool, BooleanConst.True>.Task;
            }

            return task.ContinueWith(True, TaskContinuationOptions.ExecuteSynchronously |
                                           TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                localMember = null;
                configurationTracker.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
