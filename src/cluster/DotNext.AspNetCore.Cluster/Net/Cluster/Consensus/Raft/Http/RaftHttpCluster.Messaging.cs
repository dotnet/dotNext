using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Messaging;
using Runtime.Serialization;
using static Threading.LinkedTokenSourceFactory;

internal partial class RaftHttpCluster : IOutputChannel
{
    private readonly DuplicateRequestDetector duplicationDetector;
    private volatile ImmutableList<IInputChannel> messageHandlers;
    private volatile MemberMetadata metadata;

    [MethodImpl(MethodImplOptions.Synchronized)]
    void IMessageBus.AddListener(IInputChannel handler)
        => messageHandlers = messageHandlers.Add(handler);

    [MethodImpl(MethodImplOptions.Synchronized)]
    void IMessageBus.RemoveListener(IInputChannel handler)
        => messageHandlers = messageHandlers.Remove(handler);

    async Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(responseReader);

        using var tokenSource = token.LinkTo(LifecycleToken);
        do
        {
            var leader = Leader ?? throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
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
            catch (UnexpectedStatusCodeException e) when (e.StatusCode is HttpStatusCode.BadRequest)
            {
                // keep in sync with ReceiveMessage behavior
                Logger.FailedToRouteMessage(message.Name, e);
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
        }
        while (!token.IsCancellationRequested);

        throw new OperationCanceledException(tokenSource?.CancellationOrigin ?? token);

        static async Task<TResponse> TryReceiveMessage(RaftClusterMember sender, IMessage message, IEnumerable<IInputChannel> handlers, MessageReader<TResponse> responseReader, CancellationToken token)
        {
            var responseMsg = await (handlers.TryReceiveMessage(sender, message, null, token) ?? throw new UnexpectedStatusCodeException(new NotImplementedException())).ConfigureAwait(false);
            return await responseReader(responseMsg, token).ConfigureAwait(false);
        }
    }

    async Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var tokenSource = token.LinkTo(LifecycleToken);
        do
        {
            var leader = Leader ?? throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
            try
            {
                return await (leader.IsRemote ?
                    leader.SendMessageAsync<TResponse>(message, true, token) :
                    TryReceiveMessage(leader, message, messageHandlers, token))
                    .ConfigureAwait(false);
            }
            catch (MemberUnavailableException e)
            {
                Logger.FailedToRouteMessage(message.Name, e);
            }
            catch (UnexpectedStatusCodeException e) when (e.StatusCode is HttpStatusCode.BadRequest)
            {
                // keep in sync with ReceiveMessage behavior
                Logger.FailedToRouteMessage(message.Name, e);
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
        }
        while (!token.IsCancellationRequested);

        throw new OperationCanceledException(tokenSource?.CancellationOrigin ?? token);

        static async Task<TResponse> TryReceiveMessage(RaftClusterMember sender, IMessage message, IEnumerable<IInputChannel> handlers, CancellationToken token)
        {
            var responseMsg = await (handlers.TryReceiveMessage(sender, message, null, token) ?? throw new UnexpectedStatusCodeException(new NotImplementedException())).ConfigureAwait(false);
            return await Serializable.TransformAsync<IMessage, TResponse>(responseMsg, token).ConfigureAwait(false);
        }
    }

    async Task IOutputChannel.SendSignalAsync(IMessage message, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(message);

        // keep the same message between retries for correct identification of duplicate messages
        var signal = new CustomMessage(LocalMemberId, message, true) { RespectLeadership = true };
        using var tokenSource = token.LinkTo(LifecycleToken);
        do
        {
            var leader = Leader ?? throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
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
            catch (UnexpectedStatusCodeException e) when (e.StatusCode is HttpStatusCode.ServiceUnavailable)
            {
                // keep in sync with ReceiveMessage behavior
                Logger.FailedToRouteMessage(message.Name, e);
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
        }
        while (!token.IsCancellationRequested);

        throw new OperationCanceledException(tokenSource?.CancellationOrigin ?? token);
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
        if (response.HttpContext.Features.Get<DuplicateRequestDetector>()?.IsDuplicated(request) ?? false)
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
            await CustomMessage.SaveResponseAsync(response, await task.ConfigureAwait(false), token).ConfigureAwait(false);
    }

    private Task ReceiveMessageAsync(CustomMessage message, HttpResponse response, CancellationToken token)
    {
        var sender = TryGetMember(message.Sender);

        Task result;
        if (sender is null)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            result = Task.CompletedTask;
        }
        else if (!message.RespectLeadership)
        {
            result = ReceiveMessageAsync(sender, message, response, token);
        }
        else if (LeadershipToken is { IsCancellationRequested: false } lt)
        {
            result = ReceiveMessageAsync(sender, message, response, lt, token);
        }
        else
        {
            response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            result = Task.CompletedTask;
        }

        sender?.Touch();
        return result;
    }

    private async Task ReceiveMessageAsync(RaftClusterMember sender, CustomMessage message, HttpResponse response, CancellationToken leadershipToken, CancellationToken token)
    {
        var tokenSource = token.LinkTo(leadershipToken);
        try
        {
            await ReceiveMessageAsync(sender, message, response, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        finally
        {
            tokenSource?.Dispose();
        }
    }

    private Task ReceiveMessageAsync(RaftClusterMember sender, CustomMessage message, HttpResponse response, CancellationToken token)
    {
        return message.Mode switch
        {
            CustomMessage.DeliveryMode.RequestReply => ReceiveMessageAsync(sender, message, messageHandlers, response, token),
            CustomMessage.DeliveryMode.OneWay => ReceiveOneWayMessageAsync(sender, message, messageHandlers, reliable: true, response, token),
            CustomMessage.DeliveryMode.OneWayNoAck => ReceiveOneWayMessageAsync(sender, message, messageHandlers, reliable: false, response, token),
            _ => BadRequest(response),
        };

        static Task BadRequest(HttpResponse response)
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }
    }

    private async Task VoteAsync(RequestVoteMessage request, HttpResponse response, CancellationToken token)
    {
        var sender = TryGetMember(request.Sender);

        Result<bool> result;
        if (sender is null)
        {
            result = new() { Term = Term };
        }
        else
        {
            sender.Touch();
            result = await VoteAsync(request.Sender, request.ConsensusTerm, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false);
        }

        await RequestVoteMessage.SaveResponseAsync(response, result, token).ConfigureAwait(false);
    }

    private async Task PreVoteAsync(PreVoteMessage request, HttpResponse response, CancellationToken token)
    {
        TryGetMember(request.Sender)?.Touch();
        await PreVoteMessage.SaveResponseAsync(response, await PreVoteAsync(request.Sender, request.ConsensusTerm + 1L, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false), token).ConfigureAwait(false);
    }

    private async Task ResignAsync(ResignMessage request, HttpResponse response, CancellationToken token)
    {
        var sender = TryGetMember(request.Sender);
        await ResignMessage.SaveResponseAsync(response, await ResignAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
        sender?.Touch();
    }

    private Task GetMetadataAsync(MetadataMessage request, HttpResponse response, CancellationToken token)
    {
        var sender = TryGetMember(request.Sender);
        var result = MetadataMessage.SaveResponseAsync(response, metadata, token);
        sender?.Touch();
        return result;
    }

    private async Task AppendEntriesAsync(HttpRequest request, HttpResponse response, CancellationToken token)
    {
        var message = new AppendEntriesMessage(request, out var configurationReader, out var entries);
        TryGetMember(message.Sender)?.Touch();

        if (message.ConfigurationLength > int.MaxValue)
        {
            response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
            return;
        }

        using var configuration = new ReceivedClusterConfiguration((int)message.ConfigurationLength) { Fingerprint = message.ConfigurationFingerprint };
        await configurationReader(configuration.Content, token).ConfigureAwait(false);

        await using (entries.ConfigureAwait(false))
        {
            var result = await AppendEntriesAsync(message.Sender, message.ConsensusTerm, entries, message.PrevLogIndex, message.PrevLogTerm, message.CommitIndex, configuration, message.ApplyConfiguration, token).ConfigureAwait(false);
            await AppendEntriesMessage.SaveResponseAsync(response, result, token).ConfigureAwait(false);
        }
    }

    private async Task InstallSnapshotAsync(InstallSnapshotMessage message, HttpResponse response, CancellationToken token)
    {
        TryGetMember(message.Sender)?.Touch();

        var result = await InstallSnapshotAsync(message.Sender, message.ConsensusTerm, message.Snapshot, message.Index, token).ConfigureAwait(false);
        await InstallSnapshotMessage.SaveResponseAsync(response, result, token).ConfigureAwait(false);
    }

    private async Task SynchronizeAsync(SynchronizeMessage message, HttpResponse response, CancellationToken token)
    {
        TryGetMember(message.Sender)?.Touch();

        var result = await SynchronizeAsync(message.CommitIndex, token).ConfigureAwait(false);
        await SynchronizeMessage.SaveResponseAsync(response, result, token).ConfigureAwait(false);
    }

    internal Task ProcessRequest(HttpContext context)
    {
        context.Features.Set(duplicationDetector);

        // process request
        return HttpMessage.GetMessageType(context.Request) switch
        {
            AppendEntriesMessage.MessageType => AppendEntriesAsync(context.Request, context.Response, context.RequestAborted),
            SynchronizeMessage.MessageType => SynchronizeAsync(new SynchronizeMessage(context.Request), context.Response, context.RequestAborted),
            RequestVoteMessage.MessageType => VoteAsync(new RequestVoteMessage(context.Request), context.Response, context.RequestAborted),
            PreVoteMessage.MessageType => PreVoteAsync(new PreVoteMessage(context.Request), context.Response, context.RequestAborted),
            InstallSnapshotMessage.MessageType => InstallSnapshotAsync(new InstallSnapshotMessage(context.Request), context.Response, context.RequestAborted),
            CustomMessage.MessageType => ReceiveMessageAsync(new CustomMessage(context.Request), context.Response, context.RequestAborted),
            ResignMessage.MessageType => ResignAsync(new ResignMessage(context.Request), context.Response, context.RequestAborted),
            MetadataMessage.MessageType => GetMetadataAsync(new MetadataMessage(context.Request), context.Response, context.RequestAborted),
            _ => BadRequest(context),
        };

        static Task BadRequest(HttpContext context)
        {
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