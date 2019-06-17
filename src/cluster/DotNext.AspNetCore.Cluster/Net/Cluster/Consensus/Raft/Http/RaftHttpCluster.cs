using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    internal abstract class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IExpandableCluster
    {
        private const string RaftClientHandlerName = "raftClient";
        private delegate ICollection<IPEndPoint> HostingAddressesProvider();

        private readonly IRaftClusterConfigurator configurator;
        private readonly IMessageHandler messageHandler;

        private readonly IDisposable configurationTracker;
        private volatile MemberMetadata metadata;
        private volatile ISet<IPNetwork> allowedNetworks;

        [SuppressMessage("Usage", "CA2213", Justification = "This object is disposed via RaftCluster.members collection")]
        private RaftClusterMember localMember;
        private readonly HostingAddressesProvider hostingAddresses;
        private protected readonly IHttpMessageHandlerFactory httpHandlerFactory;
        private protected readonly TimeSpan requestTimeout;


        [SuppressMessage("Reliability", "CA2000", Justification = "The member will be disposed in RaftCluster.Dispose method")]
        private RaftHttpCluster(RaftClusterMemberConfiguration config, out MemberCollection members)
            : base(config, out members)
        {
            allowedNetworks = config.AllowedNetworks;
            metadata = new MemberMetadata(config.Metadata);
            requestTimeout = TimeSpan.FromMilliseconds(config.LowerElectionTimeout);
        }

        private RaftHttpCluster(IOptionsMonitor<RaftClusterMemberConfiguration> config, IServiceProvider dependencies, out MemberCollection members)
            : this(config.CurrentValue, out members)
        {
            configurator = dependencies.GetService<IRaftClusterConfigurator>();
            messageHandler = dependencies.GetService<IMessageHandler>();
            AuditTrail = dependencies.GetService<IPersistentState>();
            hostingAddresses = dependencies.GetRequiredService<IServer>().GetHostingAddresses;
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            //track changes in configuration
            configurationTracker = config.OnChange(ConfigurationChanged);
        }

        private protected RaftHttpCluster(IServiceProvider dependencies, out MemberCollection members)
            : this(dependencies.GetRequiredService<IOptionsMonitor<RaftClusterMemberConfiguration>>(), dependencies, out members)
        {
        }

        private protected abstract RaftClusterMember CreateMember(Uri address);

        protected override ILogger Logger { get; }

        ILogger IHostingContext.Logger => Logger;

        private void ConfigurationChanged(RaftClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.AllowedNetworks;
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

        async Task<bool> IHostingContext.LocalCommitAsync(Replication.ILogEntry<LogEntryId> entry)
        {
            if (AuditTrail is null)
                throw new NotSupportedException();
            await AuditTrail.CommitAsync(entry);
            return true;
        }

        IPEndPoint IHostingContext.LocalEndpoint => localMember?.Endpoint;

        HttpMessageHandler IHostingContext.CreateHttpHandler()
            => httpHandlerFactory?.CreateHandler(RaftClientHandlerName) ?? new HttpClientHandler();

        public event ClusterChangedEventHandler MemberAdded;
        public event ClusterChangedEventHandler MemberRemoved;
        public override event ClusterMemberStatusChanged MemberStatusChanged;

        void IHostingContext.MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            => MemberStatusChanged?.Invoke(member, previousStatus, newStatus);

        public override Task StartAsync(CancellationToken token)
        {
            //detect local member
            localMember = FindMember(hostingAddresses().Contains);
            if (localMember is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
            configurator?.Initialize(this, metadata);
            return base.StartAsync(token);
        }

        public override Task StopAsync(CancellationToken token)
        {
            configurator?.Shutdown(this);
            return base.StopAsync(token);
        }

        private async Task ReceiveVote(RequestVoteMessage request, HttpResponse response)
            => await RequestVoteMessage.CreateResponse(response,
                await ReceiveVote(request.Sender, request.ConsensusTerm, request.LastEntry, ClusterMember.Represents)
                    .ConfigureAwait(false)).ConfigureAwait(false);

        private Task ReceiveHeartbeat(HeartbeatMessage request, HttpResponse response)
        {
            HeartbeatMessage.CreateResponse(response);
            return ReceiveHeartbeat(request.Sender, request.ConsensusTerm, ClusterMember.Represents);
        }

        private async Task Resign(HttpResponse response) =>
            await ResignMessage.CreateResponse(response, await ReceiveResign().ConfigureAwait(false)).ConfigureAwait(false);

        private Task GetMetadata(HttpResponse response) => MetadataMessage.CreateResponse(response, metadata);

        private async Task ReceiveEntries(AppendEntriesMessage request, HttpResponse response)
            => await AppendEntriesMessage.CreateResponse(response,
                await ReceiveEntries(request.Sender, request.ConsensusTerm, ClusterMember.Represents, request.LogEntry,
                    request.PrecedingEntry).ConfigureAwait(false)).ConfigureAwait(false);

        private async Task ReceiveMessage(CustomMessage message, HttpResponse response)
        {
            if (messageHandler is null)
            {
                response.StatusCode = (int)HttpStatusCode.NotImplemented;
            }
            else if (message.IsOneWay)
            {
                await messageHandler.ReceiveSignal(FindMember(message.Sender, ClusterMember.Represents), message.Message);
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                var reply = await messageHandler
                    .ReceiveMessage(FindMember(message.Sender, ClusterMember.Represents), message.Message)
                    .ConfigureAwait(false);
                await CustomMessage.CreateResponse(response, reply).ConfigureAwait(false);
            }
        }

        private protected Task ProcessRequest(HttpContext context)
        {
            var networks = allowedNetworks;
            //checks whether the client's address is allowed
            if (networks.Count > 0 || networks.FirstOrDefault(context.Connection.RemoteIpAddress.IsIn) is null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Task.CompletedTask;
            }

            //process request
            switch (RaftHttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return ReceiveVote(new RequestVoteMessage(context.Request), context.Response);
                case HeartbeatMessage.MessageType:
                    return ReceiveHeartbeat(new HeartbeatMessage(context.Request), context.Response);
                case ResignMessage.MessageType:
                    return Resign(context.Response);
                case MetadataMessage.MessageType:
                    return GetMetadata(context.Response);
                case AppendEntriesMessage.MessageType:
                    return ReceiveEntries(new AppendEntriesMessage(context.Request), context.Response);
                case CustomMessage.MessageType:
                    return ReceiveMessage(new CustomMessage(context.Request), context.Response);
                default:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return Task.CompletedTask;
            }
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
