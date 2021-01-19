using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using static System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using static Threading.LinkedTokenSourceFactory;

    internal partial class RaftHttpCluster : IOutputChannel
    {
        private static readonly Func<RaftClusterMember, IPEndPoint, bool> MatchByEndPoint = IsMatchedByEndPoint;
        private readonly DuplicateRequestDetector duplicationDetector;
        private volatile IImmutableSet<IPNetwork> allowedNetworks;
        private volatile ImmutableList<IInputChannel> messageHandlers;
        private volatile MemberMetadata metadata;

        private static bool IsMatchedByEndPoint(RaftClusterMember member, IPEndPoint endPoint)
            => member.Endpoint.Equals(endPoint);

        [MethodImpl(MethodImplOptions.Synchronized)]
        void IMessageBus.AddListener(IInputChannel handler)
            => messageHandlers = messageHandlers.Add(handler);

        [MethodImpl(MethodImplOptions.Synchronized)]
        void IMessageBus.RemoveListener(IInputChannel handler)
            => messageHandlers = messageHandlers.Remove(handler);

        async Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
        {
            static async Task<TResponse> TryReceiveMessage(RaftClusterMember sender, IMessage message, IEnumerable<IInputChannel> handlers, MessageReader<TResponse> responseReader, CancellationToken token)
            {
                var responseMsg = await (handlers.TryReceiveMessage(sender, message, null, token) ?? throw new UnexpectedStatusCodeException(new NotImplementedException())).ConfigureAwait(false);
                return await responseReader(responseMsg, token).ConfigureAwait(false);
            }

            var tokenSource = token.LinkTo(Token);
            try
            {
                do
                {
                    var leader = Leader;
                    if (leader is null)
                        throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
                    try
                    {
                        return await (leader.IsRemote ?
                            leader.SendMessageAsync(message, responseReader, true, token) :
                            TryReceiveMessage(leader, message, messageHandlers, responseReader, token))
                            .ConfigureAwait(false);
                    }
                    catch (MemberUnavailableException e)
                    {
                        Logger.FailedToRouteMessage(message.Name, e);
                    }
                    catch (UnexpectedStatusCodeException e) when (e.StatusCode == HttpStatusCode.BadRequest)
                    {
                        // keep in sync with ReceiveMessage behavior
                        Logger.FailedToRouteMessage(message.Name, e);
                    }
                }
                while (!token.IsCancellationRequested);
            }
            finally
            {
                tokenSource?.Dispose();
            }

            throw new OperationCanceledException(token);
        }

        async Task IOutputChannel.SendSignalAsync(IMessage message, CancellationToken token)
        {
            Assert(localMember is not null);

            // keep the same message between retries for correct identification of duplicate messages
            var signal = new CustomMessage(localMember, message, true) { RespectLeadership = true };
            var tokenSource = token.LinkTo(Token);
            try
            {
                do
                {
                    var leader = Leader;
                    if (leader is null)
                        throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
                    try
                    {
                        var response = leader.IsRemote ?
                            leader.SendSignalAsync(signal, token) :
                            (messageHandlers.TryReceiveSignal(leader, signal.Message, null, token) ?? throw new UnexpectedStatusCodeException(new NotImplementedException()));
                        await response.ConfigureAwait(false);
                        return;
                    }
                    catch (MemberUnavailableException e)
                    {
                        Logger.FailedToRouteMessage(message.Name, e);
                    }
                    catch (UnexpectedStatusCodeException e) when (e.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        // keep in sync with ReceiveMessage behavior
                        Logger.FailedToRouteMessage(message.Name, e);
                    }
                }
                while (!token.IsCancellationRequested);
            }
            finally
            {
                tokenSource?.Dispose();
            }

            throw new OperationCanceledException(token);
        }

        IOutputChannel IMessageBus.LeaderRouter => this;

        private static async Task ReceiveOneWayMessageFastAckAsync(ISubscriber sender, IMessage message, IEnumerable<IInputChannel> handlers, HttpResponse response, CancellationToken token)
        {
            IInputChannel? handler = handlers.FirstOrDefault(message.IsSignalSupported);
            if (handler is null)
                return;
            IBufferedMessage buffered = message.Length.TryGetValue(out var length) && length < FileMessage.MinSize ?
                new InMemoryMessage(message.Name, message.Type, Convert.ToInt32(length)) :
                new FileMessage(message.Name, message.Type);
            await buffered.LoadFromAsync(message, token).ConfigureAwait(false);
            buffered.PrepareForReuse();
            response.OnCompleted(ReceiveSignal);

            // Do not use response.RegisterForDispose() because it is calling earlier than
            // OnCompleted callback
            async Task ReceiveSignal()
            {
                using (buffered)
                    await handler.ReceiveSignal(sender, buffered, null, token).ConfigureAwait(false);
            }
        }

        private static Task ReceiveOneWayMessageAsync(ISubscriber sender, CustomMessage request, IEnumerable<IInputChannel> handlers, bool reliable, HttpResponse response, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status204NoContent;

            // drop duplicated request
            if (response.HttpContext.Features.Get<DuplicateRequestDetector>().IsDuplicated(request))
                return Task.CompletedTask;
            Task? task = reliable ?
                handlers.TryReceiveSignal(sender, request.Message, response.HttpContext, token) :
                ReceiveOneWayMessageFastAckAsync(sender, request.Message, handlers, response, token);
            if (task is null)
            {
                response.StatusCode = StatusCodes.Status501NotImplemented;
                task = Task.CompletedTask;
            }

            return task;
        }

        private static async Task ReceiveMessageAsync(ISubscriber sender, CustomMessage request, IEnumerable<IInputChannel> handlers, HttpResponse response, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            var task = handlers.TryReceiveMessage(sender, request.Message, response.HttpContext, token);
            if (task is null)
                response.StatusCode = StatusCodes.Status501NotImplemented;
            else
                await CustomMessage.SaveResponse(response, await task.ConfigureAwait(false), token).ConfigureAwait(false);
        }

        private Task ReceiveMessageAsync(CustomMessage message, HttpResponse response, CancellationToken token)
        {
            var sender = FindMember(MatchByEndPoint, message.Sender);
            var task = Task.CompletedTask;
            if (sender is null)
            {
                response.StatusCode = StatusCodes.Status404NotFound;
            }
            else if (!message.RespectLeadership || IsLeaderLocal)
            {
                switch (message.Mode)
                {
                    case CustomMessage.DeliveryMode.RequestReply:
                        task = ReceiveMessageAsync(sender, message, messageHandlers, response, token);
                        break;
                    case CustomMessage.DeliveryMode.OneWay:
                        task = ReceiveOneWayMessageAsync(sender, message, messageHandlers, true, response, token);
                        break;
                    case CustomMessage.DeliveryMode.OneWayNoAck:
                        task = ReceiveOneWayMessageAsync(sender, message, messageHandlers, false, response, token);
                        break;
                    default:
                        response.StatusCode = StatusCodes.Status400BadRequest;
                        break;
                }
            }
            else
            {
                response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }

            sender?.Touch();
            return task;
        }

        private async Task ReceiveVoteAsync(RequestVoteMessage request, HttpResponse response, CancellationToken token)
        {
            var sender = FindMember(MatchByEndPoint, request.Sender);
            if (sender is null)
            {
                await request.SaveResponse(response, new Result<bool>(Term, false), token).ConfigureAwait(false);
            }
            else
            {
                await request.SaveResponse(response, await ReceiveVoteAsync(sender, request.ConsensusTerm, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false), token).ConfigureAwait(false);
                sender.Touch();
            }
        }

        private async Task ReceivePreVoteAsync(PreVoteMessage request, HttpResponse response, CancellationToken token)
        {
            var sender = FindMember(MatchByEndPoint, request.Sender);
            if (sender is null)
            {
                await request.SaveResponse(response, new Result<bool>(Term, false), token).ConfigureAwait(false);
            }
            else
            {
                await request.SaveResponse(response, await ReceivePreVoteAsync(request.ConsensusTerm + 1L, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false), token).ConfigureAwait(false);
                sender.Touch();
            }
        }

        private async Task ResignAsync(ResignMessage request, HttpResponse response, CancellationToken token)
        {
            var sender = FindMember(MatchByEndPoint, request.Sender);
            await request.SaveResponse(response, await ReceiveResignAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            sender?.Touch();
        }

        private Task GetMetadataAsync(MetadataMessage request, HttpResponse response, CancellationToken token)
        {
            var sender = FindMember(MatchByEndPoint, request.Sender);
            var result = request.SaveResponse(response, metadata, token);
            sender?.Touch();
            return result;
        }

        private async Task ReceiveEntriesAsync(HttpRequest request, HttpResponse response, CancellationToken token)
        {
            var message = new AppendEntriesMessage(request, out var entries);
            await using (entries)
            {
                var sender = FindMember(MatchByEndPoint, message.Sender);
                if (sender is null)
                    response.StatusCode = StatusCodes.Status404NotFound;
                else
                    await message.SaveResponse(response, await ReceiveEntriesAsync(sender, message.ConsensusTerm, entries, message.PrevLogIndex, message.PrevLogTerm, message.CommitIndex, token).ConfigureAwait(false), token).ConfigureAwait(false);
            }
        }

        private async Task InstallSnapshot(InstallSnapshotMessage message, HttpResponse response, CancellationToken token)
        {
            var sender = FindMember(MatchByEndPoint, message.Sender);
            if (sender is null)
                response.StatusCode = StatusCodes.Status404NotFound;
            else
                await message.SaveResponse(response, await ReceiveSnapshotAsync(sender, message.ConsensusTerm, message.Snapshot, message.Index, token).ConfigureAwait(false), token).ConfigureAwait(false);
        }

        internal Task ProcessRequest(HttpContext context)
        {
            // this check allows to prevent situation when request comes earlier than initialization
            if (localMember is null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsync(ExceptionMessages.UnresolvedLocalMember, context.RequestAborted);
            }

            var networks = allowedNetworks;

            // checks whether the client's address is allowed
            if (networks.Count > 0 && networks.FirstOrDefault(context.Connection.RemoteIpAddress.IsIn) is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Features.Set(duplicationDetector);

            // process request
            switch (HttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return ReceiveVoteAsync(new RequestVoteMessage(context.Request), context.Response, context.RequestAborted);
                case PreVoteMessage.MessageType:
                    return ReceivePreVoteAsync(new PreVoteMessage(context.Request), context.Response, context.RequestAborted);
                case ResignMessage.MessageType:
                    return ResignAsync(new ResignMessage(context.Request), context.Response, context.RequestAborted);
                case MetadataMessage.MessageType:
                    return GetMetadataAsync(new MetadataMessage(context.Request), context.Response, context.RequestAborted);
                case AppendEntriesMessage.MessageType:
                    return ReceiveEntriesAsync(context.Request, context.Response, context.RequestAborted);
                case CustomMessage.MessageType:
                    return ReceiveMessageAsync(new CustomMessage(context.Request), context.Response, context.RequestAborted);
                case InstallSnapshotMessage.MessageType:
                    return InstallSnapshot(new InstallSnapshotMessage(context.Request), context.Response, context.RequestAborted);
                default:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return Task.CompletedTask;
            }
        }

        private bool TryGetTimeout(Type messageType, out TimeSpan timeout)
        {
            if (typeof(RaftHttpMessage).IsAssignableFrom(messageType))
            {
                timeout = raftRpcTimeout;
                return true;
            }

            timeout = default;
            return false;
        }

        bool IHostingContext.TryGetTimeout<TMessage>(out TimeSpan timeout)
            => TryGetTimeout(typeof(TMessage), out timeout);
    }
}