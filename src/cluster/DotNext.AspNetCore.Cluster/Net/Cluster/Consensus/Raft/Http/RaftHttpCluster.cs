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

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    internal abstract class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IExpandableCluster, IMessageBus
    {
        private readonly IRaftClusterConfigurator configurator;
        private readonly IMessageHandler messageHandler;

        private readonly IDisposable configurationTracker;
        private volatile MemberMetadata metadata;
        private volatile ISet<IPNetwork> allowedNetworks;

        [SuppressMessage("Usage", "CA2213", Justification = "This object is disposed via RaftCluster.members collection")]
        private RaftClusterMember localMember;

        private readonly IHttpMessageHandlerFactory httpHandlerFactory;
        private readonly TimeSpan requestTimeout;
        private readonly DuplicateRequestDetector duplicationDetector;
        private readonly bool openConnectionForEachRequest;
        private readonly string clientHandlerName;
        private readonly HttpVersion protocolVersion;

        [SuppressMessage("Reliability", "CA2000", Justification = "The member will be disposed in RaftCluster.Dispose method")]
        private RaftHttpCluster(RaftClusterMemberConfiguration config, out MutableMemberCollection members)
            : base(config, out members)
        {
            openConnectionForEachRequest = config.OpenConnectionForEachRequest;
            allowedNetworks = config.AllowedNetworks;
            metadata = new MemberMetadata(config.Metadata);
            requestTimeout = TimeSpan.FromMilliseconds(config.UpperElectionTimeout);
            duplicationDetector = new DuplicateRequestDetector(config.RequestJournal);
            clientHandlerName = config.ClientHandlerName;
            protocolVersion = config.ProtocolVersion;
        }

        private RaftHttpCluster(IOptionsMonitor<RaftClusterMemberConfiguration> config, IServiceProvider dependencies, out MutableMemberCollection members)
            : this(config.CurrentValue, out members)
        {
            configurator = dependencies.GetService<IRaftClusterConfigurator>();
            messageHandler = dependencies.GetService<IMessageHandler>();
            AuditTrail = dependencies.GetService<IPersistentState>() ?? new InMemoryAuditTrail();
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            var loggerFactory = dependencies.GetRequiredService<ILoggerFactory>();
            Logger = loggerFactory.CreateLogger(GetType());
            Metrics = dependencies.GetService<MetricsCollector>();
            //track changes in configuration
            configurationTracker = config.OnChange(ConfigurationChanged);
        }

        private protected RaftHttpCluster(IServiceProvider dependencies, out MutableMemberCollection members)
            : this(dependencies.GetRequiredService<IOptionsMonitor<RaftClusterMemberConfiguration>>(), dependencies, out members)
        {
        }

        private protected void ConfigureMember(RaftClusterMember member)
        {
            member.Timeout = requestTimeout;
            member.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
            member.Metrics = Metrics as IHttpClientMetrics;
            member.ProtocolVersion = protocolVersion;
        }

        private protected abstract RaftClusterMember CreateMember(Uri address);

        protected override ILogger Logger { get; }

        ILogger IHostingContext.Logger => Logger;

        IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

        ISubscriber IMessageBus.Leader => Leader;

        private async void ConfigurationChanged(RaftClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.AllowedNetworks;
            await ChangeMembersAsync(members =>
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
            }).ConfigureAwait(false);
        }

        async Task<TResponse> IMessageBus.SendMessageToLeaderAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
        {
            if (!token.CanBeCanceled)
                token = Token;
            do
            {
                var leader = Leader;
                if (leader is null)
                    throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
                try
                {
                    return await leader.SendMessageAsync(message, responseReader, true, token).ConfigureAwait(false);
                }
                catch (MemberUnavailableException)
                {
                }
                catch (UnexpectedStatusCodeException e) when (e.StatusCode == HttpStatusCode.BadRequest) //keep in sync with ReceiveMessage behavior
                {
                }
            }
            while (!token.IsCancellationRequested);
            throw new OperationCanceledException(token);
        }

        async Task IMessageBus.SendSignalToLeaderAsync(IMessage message, CancellationToken token)
        {
            if (!token.CanBeCanceled)
                token = Token;
            //keep the same message between retries for correct identification of duplicate messages
            var signal = new CustomMessage(localMember.Endpoint, message, true) { RespectLeadership = true };
            do
            {
                var leader = Leader;
                if (leader is null)
                    throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
                try
                {
                    await leader.SendSignalAsync(signal, token).ConfigureAwait(false);
                }
                catch (MemberUnavailableException)
                {
                    continue;
                }
                catch (UnexpectedStatusCodeException e) when (e.StatusCode == HttpStatusCode.ServiceUnavailable) //keep in sync with ReceiveMessage behavior
                {
                    continue;
                }
                return;
            }
            while (!token.IsCancellationRequested);
            throw new OperationCanceledException(token);
        }

        IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

        bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        IPEndPoint IHostingContext.LocalEndpoint => localMember?.Endpoint;

        HttpMessageHandler IHostingContext.CreateHttpHandler()
            => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new HttpClientHandler();

        public event ClusterChangedEventHandler MemberAdded;
        public event ClusterChangedEventHandler MemberRemoved;

        private protected abstract Predicate<RaftClusterMember> LocalMemberFinder { get; }


        public override Task StartAsync(CancellationToken token)
        {
            //detect local member
            localMember = FindMember(LocalMemberFinder);
            if (localMember is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
            configurator?.Initialize(this, metadata);
            return base.StartAsync(token);
        }

        public override Task StopAsync(CancellationToken token)
        {
            configurator?.Shutdown(this);
            duplicationDetector.Trim(100);
            return base.StopAsync(token);
        }

        private async Task ReceiveVote(RequestVoteMessage request, HttpResponse response)
        {
            var sender = FindMember(request.Sender.Represents);
            if (sender is null)
                await request.SaveResponse(response, new Result<bool>(Term, false), Token).ConfigureAwait(false);
            else
            {
                await request.SaveResponse(response,
                    await ReceiveVote(sender, request.ConsensusTerm, request.LastLogIndex, request.LastLogTerm)
                        .ConfigureAwait(false), Token).ConfigureAwait(false);
                sender.Touch();
            }
        }

        private async Task Resign(ResignMessage request, HttpResponse response)
        {
            var sender = FindMember(request.Sender.Represents);
            await request.SaveResponse(response, await ReceiveResign().ConfigureAwait(false), Token).ConfigureAwait(false);
            sender?.Touch();
        }

        private Task GetMetadata(MetadataMessage request, HttpResponse response)
        {
            var sender = FindMember(request.Sender.Represents);
            var result = request.SaveResponse(response, metadata, Token);
            sender?.Touch();
            return result;
        }

        private async Task ReceiveEntries(HttpRequest request, HttpResponse response)
        {
            var message = new AppendEntriesMessage(request, out var entries);
            var sender = FindMember(message.Sender.Represents);
            if (sender is null)
                response.StatusCode = StatusCodes.Status404NotFound;
            else
                await message.SaveResponse(response, await ReceiveEntries(sender, message.ConsensusTerm,
                    entries, message.PrevLogIndex,
                    message.PrevLogTerm, message.CommitIndex).ConfigureAwait(false), Token).ConfigureAwait(false);
        }

        [SuppressMessage("Reliability", "CA2000", Justification = "Buffered message will be destroyed in OnCompleted method")]
        private static async Task ReceiveOneWayMessageFastAck(ISubscriber sender, IMessage message, IMessageHandler handler, HttpResponse response, CancellationToken token)
        {
            const long maxSize = 10 * 1024;   //10 KB
            var length = message.Length;
            IDisposableMessage buffered;
            if (length.HasValue && length.Value < maxSize)
                buffered = await StreamMessage.CreateBufferedMessageAsync(message, token).ConfigureAwait(false);
            else
            {
                var file = new FileMessage(message.Name, message.Type);
                await message.CopyToAsync(file, token).ConfigureAwait(false);
                file.Position = 0;
                buffered = file;
            }
            response.OnCompleted(async delegate ()
            {
                using (buffered)
                    await handler.ReceiveSignal(sender, buffered, null).ConfigureAwait(false);
            });
        }

        private static Task ReceiveOneWayMessage(ISubscriber sender, CustomMessage request, IMessageHandler handler, bool reliable, HttpResponse response, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status204NoContent;
            //drop duplicated request
            if (response.HttpContext.Features.Get<DuplicateRequestDetector>().IsDuplicated(request))
                return Task.CompletedTask;
            return reliable ? handler.ReceiveSignal(sender, request.Message, response.HttpContext) : ReceiveOneWayMessageFastAck(sender, request.Message, handler, response, token);
        }

        private static async Task ReceiveMessage(ISubscriber sender, CustomMessage request, IMessageHandler handler, HttpResponse response, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            await request.SaveResponse(response, await handler.ReceiveMessage(sender, request.Message, response.HttpContext).ConfigureAwait(false), token).ConfigureAwait(false);
        }

        private Task ReceiveMessage(CustomMessage message, HttpResponse response)
        {
            var sender = FindMember(message.Sender.Represents);
            Task task;
            if (sender is null)
            {
                response.StatusCode = StatusCodes.Status404NotFound;
                task = Task.CompletedTask;
            }
            else if (messageHandler is null)
            {
                response.StatusCode = StatusCodes.Status501NotImplemented;
                task = Task.CompletedTask;
            }
            else if (!message.RespectLeadership || IsLeaderLocal)
                switch (message.Mode)
                {
                    case CustomMessage.DeliveryMode.RequestReply:
                        task = ReceiveMessage(sender, message, messageHandler, response, Token);
                        break;
                    case CustomMessage.DeliveryMode.OneWay:
                        task = ReceiveOneWayMessage(sender, message, messageHandler, true, response, Token);
                        break;
                    case CustomMessage.DeliveryMode.OneWayNoAck:
                        task = ReceiveOneWayMessage(sender, message, messageHandler, false, response, Token);
                        break;
                    default:
                        response.StatusCode = StatusCodes.Status400BadRequest;
                        task = Task.CompletedTask;
                        break;
                }
            else
            {
                response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                task = Task.CompletedTask;
            }

            sender?.Touch();
            return task;
        }

        private async Task InstallSnapshot(InstallSnapshotMessage message, HttpResponse response)
        {
            var sender = FindMember(message.Sender.Represents);
            if (sender is null)
                response.StatusCode = StatusCodes.Status404NotFound;
            else
                await message.SaveResponse(response, await ReceiveSnapshot(sender, message.ConsensusTerm, message.Snapshot, message.Index).ConfigureAwait(false), Token).ConfigureAwait(false);
        }

        internal Task ProcessRequest(HttpContext context)
        {
            //this check allows to prevent situation when request comes earlier than initialization 
            if (localMember is null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsync(ExceptionMessages.UnresolvedLocalMember, Token);
            }
            var networks = allowedNetworks;
            //checks whether the client's address is allowed
            if (networks.Count > 0 && networks.FirstOrDefault(context.Connection.RemoteIpAddress.IsIn) is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            context.Features.Set(duplicationDetector);
            //process request
            switch (HttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return ReceiveVote(new RequestVoteMessage(context.Request), context.Response);
                case ResignMessage.MessageType:
                    return Resign(new ResignMessage(context.Request), context.Response);
                case MetadataMessage.MessageType:
                    return GetMetadata(new MetadataMessage(context.Request), context.Response);
                case AppendEntriesMessage.MessageType:
                    return ReceiveEntries(context.Request, context.Response);
                case CustomMessage.MessageType:
                    return ReceiveMessage(new CustomMessage(context.Request), context.Response);
                case InstallSnapshotMessage.MessageType:
                    return InstallSnapshot(new InstallSnapshotMessage(context.Request), context.Response);
                default:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return Task.CompletedTask;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                localMember = null;
                configurationTracker.Dispose();
                duplicationDetector.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
