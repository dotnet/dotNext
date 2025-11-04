using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Collections.Specialized;
using Membership;
using Messaging;
using Net.Http;
using Runtime.Serialization;
using Timestamp = Diagnostics.Timestamp;

internal sealed class RaftClusterMember : HttpPeerClient, IRaftClusterMember, ISubscriber
{
    private static readonly Histogram<double> ResponseTimeMeter;

    internal const string DefaultProtocolPath = "/cluster-consensus/raft";
    private const string UserAgent = "Raft.NET";

    private readonly Uri? resourcePath;
    private readonly IHostingContext context;
    internal readonly ClusterMemberId Id;
    internal readonly UriEndPoint EndPoint;
    private readonly KeyValuePair<string, object?> cachedRemoteAddressAttribute;
    private volatile ClusterMemberStatus status;
    private volatile MemberMetadata? metadata;
    private InvocationList<Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>>> memberStatusChanged;
    private IRaftClusterMember.ReplicationState state;

    static RaftClusterMember()
    {
        var meter = new Meter("DotNext.Net.Cluster.Consensus.Raft.Client");
        ResponseTimeMeter = meter.CreateHistogram<double>("response-time", unit: "ms", description: "Response Time");
    }

    internal RaftClusterMember(IHostingContext context, UriEndPoint remoteMember)
        : base(remoteMember.Uri, context.CreateHttpHandler(), true)
    {
        this.context = context;
        EndPoint = remoteMember;
        cachedRemoteAddressAttribute = new(IRaftClusterMember.RemoteAddressMeterAttributeName, remoteMember.ToString());
        Id = ClusterMemberId.FromEndPoint(remoteMember);
        resourcePath = remoteMember.Uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped) is { Length: > 0 }
            ? null
            : new(DefaultProtocolPath, UriKind.Relative);
        DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, (GetType().Assembly.GetName().Version ?? new()).ToString()));
        IsRemote = Id != context.LocalMemberId;
    }

    event Action<ClusterMemberStatusChangedEventArgs> IClusterMember.MemberStatusChanged
    {
        add => memberStatusChanged += value;
        remove => memberStatusChanged -= value;
    }

    internal void Touch() => Status = ClusterMemberStatus.Available;

    private async Task<TResponse> SendAsync<TResponse, TMessage>(TMessage message, CancellationToken token)
        where TMessage : class, IHttpMessage<TResponse>
    {
        context.Logger.SendingRequestToMember(EndPoint, TMessage.MessageType);
        var request = new HttpRequestMessage
        {
            RequestUri = resourcePath,
            Version = DefaultRequestVersion,
            VersionPolicy = DefaultVersionPolicy,
        };

        HttpMessage.SetMessageType<TMessage>(request);
        message.PrepareRequest(request);

        // setup additional timeout control token needed if actual timeout
        // doesn't match to HttpClient.Timeout
        CancellationTokenSource? timeoutControl;
        CancellationToken tokenWithTimeout;
        if (context.TryGetTimeout<TMessage>(out var timeout) && timeout < Timeout)
        {
            timeoutControl = CancellationTokenSource.CreateLinkedTokenSource(token);
            tokenWithTimeout = timeoutControl.Token;
            timeoutControl.CancelAfter(timeout);
        }
        else
        {
            timeoutControl = null;
            tokenWithTimeout = token;
        }

        // do HTTP request and use token associated with custom timeout
        var response = default(HttpResponseMessage);
        var timeStamp = new Timestamp();
        try
        {
            response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, tokenWithTimeout)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await message.ParseResponseAsync(response, tokenWithTimeout).ConfigureAwait(false);
            Touch();
            return result;
        }
        catch (HttpRequestException e)
        {
            if (response is null || TMessage.IsMemberUnavailable(e.StatusCode))
                throw MemberUnavailable(e);

            throw new UnexpectedStatusCodeException(response, e);
        }
        catch (OperationCanceledException e) when (e.InnerException is TimeoutException timeoutEx)
        {
            // This handler catches timeout in .NET 5 or later.
            // See blog post https://devblogs.microsoft.com/dotnet/net-5-new-networking-improvements/ for
            // more info about handling timeouts in .NET 5
            throw MemberUnavailable(timeoutEx);
        }
        catch (OperationCanceledException e) when (token.IsCancellationRequested is false)
        {
            // This handler catches inability to connect to the remote host on Windows platform.
            // On Linux, this situation is handled by handler for HttpRequestException
            throw MemberUnavailable(e);
        }
        finally
        {
            timeoutControl?.Dispose();
            response?.Content?.Dispose();
            response?.Dispose();
            request.Dispose();

            var responseTime = timeStamp.ElapsedMilliseconds;
            ResponseTimeMeter.Record(
                responseTime,
                new(IRaftClusterMember.MessageTypeAttributeName, TMessage.MessageType),
                cachedRemoteAddressAttribute);
        }

        MemberUnavailableException MemberUnavailable(Exception e)
        {
            context.Logger.MemberUnavailable(EndPoint, e);
            Status = ClusterMemberStatus.Unavailable;
            return new MemberUnavailableException(this, innerException: e);
        }
    }

    ValueTask IRaftClusterMember.CancelPendingRequestsAsync()
    {
        var result = ValueTask.CompletedTask;
        try
        {
            CancelPendingRequests();
        }
        catch (Exception e)
        {
            result = ValueTask.FromException(e);
        }

        return result;
    }

    Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Result<bool>>(token)
            : IsRemote
            ? SendAsync<Result<bool>, RequestVoteMessage>(new RequestVoteMessage(context.LocalMemberId, term, lastLogIndex, lastLogTerm), token)
            : Task.FromResult<Result<bool>>(new() { Term = term, Value = true });
    }

    Task<Result<PreVoteResult>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Result<PreVoteResult>>(token)
            : IsRemote
            ? SendAsync<Result<PreVoteResult>, PreVoteMessage>(new PreVoteMessage(context.LocalMemberId, term, lastLogIndex, lastLogTerm), token)
            : Task.FromResult<Result<PreVoteResult>>(new() { Term = term, Value = PreVoteResult.Accepted });
    }

    Task<bool> IClusterMember.ResignAsync(CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<bool>(token)
            : SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalMemberId), token);
    }

    Task<Result<HeartbeatResult>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(
        long term,
        TList entries,
        long prevLogIndex,
        long prevLogTerm,
        long commitIndex,
        IClusterConfiguration configuration,
        bool applyConfig,
        CancellationToken token)
    {
        Task<Result<HeartbeatResult>> result;

        if (token.IsCancellationRequested)
        {
            result = Task.FromCanceled<Result<HeartbeatResult>>(token);
        }
        else if (IsRemote)
        {
            AppendEntriesMessage<TEntry, TList> message;

            // See bug https://github.com/dotnet/dotNext/issues/155
            try
            {
                message = new(context.LocalMemberId, term, prevLogIndex, prevLogTerm, commitIndex, entries, configuration, applyConfig)
                {
                    UseOptimizedTransfer = context.UseEfficientTransferOfLogEntries,
                };
            }
            catch (Exception e)
            {
                result = Task.FromException<Result<HeartbeatResult>>(e);
                goto exit;
            }

            result = SendAsync<Result<HeartbeatResult>, AppendEntriesMessage<TEntry, TList>>(message, token);
        }
        else
        {
            result = Task.FromResult<Result<HeartbeatResult>>(new() { Term = term, Value = HeartbeatResult.ReplicatedWithLeaderTerm });
        }

    exit:
        return result;
    }

    Task<Result<HeartbeatResult>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Result<HeartbeatResult>>(token)
            : IsRemote
            ? SendAsync<Result<HeartbeatResult>, InstallSnapshotMessage>(new InstallSnapshotMessage(context.LocalMemberId, term, snapshotIndex, snapshot), token)
            : Task.FromResult<Result<HeartbeatResult>>(new() { Term = term, Value = HeartbeatResult.ReplicatedWithLeaderTerm });
    }

    async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
    {
        if (!IsRemote)
            return context.Metadata;

        if (metadata is null || refresh)
            metadata = await SendAsync<MemberMetadata, MetadataMessage>(new MetadataMessage(context.LocalMemberId), token).ConfigureAwait(false);

        return metadata;
    }

    Task<long?> IRaftClusterMember.SynchronizeAsync(long commitIndex, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<long?>(token)
            : IsRemote
            ? SendAsync<long?, SynchronizeMessage>(new SynchronizeMessage(context.LocalMemberId, commitIndex), token)
            : Task.FromResult<long?>(null);
    }

    EndPoint IPeer.EndPoint => EndPoint;

    ClusterMemberId IClusterMember.Id => Id;

    bool IClusterMember.IsLeader => context.IsLeader(this);

    public bool IsRemote { get; }

    public ClusterMemberStatus Status
    {
        get => IsRemote ? status : ClusterMemberStatus.Available;

#pragma warning disable CS0420
        private set => IClusterMember.OnMemberStatusChanged(this, ref status, value, memberStatusChanged);
#pragma warning restore CS0420
    }

    internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, bool respectLeadership, CancellationToken token)
        => SendAsync<TResponse, CustomMessage<TResponse>>(new(context.LocalMemberId, message, responseReader) { RespectLeadership = respectLeadership }, token);

    Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
        => SendMessageAsync(message, responseReader, false, token);

    internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, bool respectLeadership, CancellationToken token)
        where TResponse : ISerializable<TResponse>
        => SendAsync<TResponse, CustomSerializableMessage<TResponse>>(new(context.LocalMemberId, message) { RespectLeadership = respectLeadership }, token);

    Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, CancellationToken token)
        => SendMessageAsync<TResponse>(message, false, token);

    internal Task SendSignalAsync(CustomMessage message, CancellationToken token) =>
        SendAsync<IMessage?, CustomMessage>(message, token);

    Task ISubscriber.SendSignalAsync(IMessage message, bool requiresConfirmation, CancellationToken token)
    {
        var request = new CustomMessage(context.LocalMemberId, message, requiresConfirmation);
        return SendSignalAsync(request, token);
    }

    ref IRaftClusterMember.ReplicationState IRaftClusterMember.State => ref state;

    public override string? ToString() => BaseAddress?.ToString();
}