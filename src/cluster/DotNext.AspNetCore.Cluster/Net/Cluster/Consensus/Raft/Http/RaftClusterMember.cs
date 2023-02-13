using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Collections.Specialized;
using Membership;
using Messaging;
using Net.Http;
using Runtime.Serialization;
using Threading;
using IClientMetricsCollector = Metrics.IClientMetricsCollector;
using Timestamp = Diagnostics.Timestamp;

internal sealed class RaftClusterMember : HttpPeerClient, IRaftClusterMember, ISubscriber
{
    internal const string DefaultProtocolPath = "/cluster-consensus/raft";
    private const string UserAgent = "Raft.NET";

    private readonly Uri? resourcePath;
    private readonly IHostingContext context;
    internal readonly ClusterMemberId Id;
    internal readonly UriEndPoint EndPoint;
    private AtomicEnum<ClusterMemberStatus> status;
    private volatile MemberMetadata? metadata;
    private InvocationList<Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>>> memberStatusChanged;
    private long nextIndex, fingerprint;
    internal IClientMetricsCollector? Metrics;

    internal RaftClusterMember(IHostingContext context, UriEndPoint remoteMember, in ClusterMemberId id)
        : base(remoteMember.Uri, context.CreateHttpHandler(), true)
    {
        this.context = context;
        status = new(ClusterMemberStatus.Unknown);
        EndPoint = remoteMember;
        Id = id;
        resourcePath = remoteMember.Uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped) is { Length: > 0 }
            ? null
            : new(DefaultProtocolPath, UriKind.Relative);
        DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, (GetType().Assembly.GetName().Version ?? new()).ToString()));
    }

    event Action<ClusterMemberStatusChangedEventArgs> IClusterMember.MemberStatusChanged
    {
        add => memberStatusChanged += value;
        remove => memberStatusChanged -= value;
    }

    internal void Touch() => Status = ClusterMemberStatus.Available;

    [RequiresPreviewFeatures]
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
            Metrics?.ReportResponseTime(timeStamp.Elapsed, TMessage.MessageType, EndPoint);
        }

        MemberUnavailableException MemberUnavailable(Exception e)
        {
            context.Logger.MemberUnavailable(EndPoint, e);
            Status = ClusterMemberStatus.Unavailable;
            return new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
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

    [RequiresPreviewFeatures]
    Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => IsRemote
            ? SendAsync<Result<bool>, RequestVoteMessage>(new RequestVoteMessage(context.LocalMember, term, lastLogIndex, lastLogTerm), token)
            : Task.FromResult(new Result<bool>(term, true));

    [RequiresPreviewFeatures]
    Task<Result<PreVoteResult>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => IsRemote
            ? SendAsync<Result<PreVoteResult>, PreVoteMessage>(new PreVoteMessage(context.LocalMember, term, lastLogIndex, lastLogTerm), token)
            : Task.FromResult(new Result<PreVoteResult>(term, PreVoteResult.Accepted));

    [RequiresPreviewFeatures]
    Task<bool> IClusterMember.ResignAsync(CancellationToken token)
        => SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalMember), token);

    [RequiresPreviewFeatures]
    Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(
        long term,
        TList entries,
        long prevLogIndex,
        long prevLogTerm,
        long commitIndex,
        IClusterConfiguration configuration,
        bool applyConfig,
        CancellationToken token)
    {
        return IsRemote
            ? SendAsync<Result<bool>, AppendEntriesMessage<TEntry, TList>>(new AppendEntriesMessage<TEntry, TList>(context.LocalMember, term, prevLogIndex, prevLogTerm, commitIndex, entries, configuration, applyConfig) { UseOptimizedTransfer = context.UseEfficientTransferOfLogEntries }, token)
            : Task.FromResult(new Result<bool>(term, true));
    }

    [RequiresPreviewFeatures]
    Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        => IsRemote
            ? SendAsync<Result<bool>, InstallSnapshotMessage>(new InstallSnapshotMessage(context.LocalMember, term, snapshotIndex, snapshot), token)
            : Task.FromResult(new Result<bool>(term, true));

    [RequiresPreviewFeatures]
    async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
    {
        if (!IsRemote)
            return context.Metadata;

        if (metadata is null || refresh)
            metadata = await SendAsync<MemberMetadata, MetadataMessage>(new MetadataMessage(context.LocalMember), token).ConfigureAwait(false);

        return metadata;
    }

    [RequiresPreviewFeatures]
    Task<long?> IRaftClusterMember.SynchronizeAsync(long commitIndex, CancellationToken token)
        => IsRemote ? SendAsync<long?, SynchronizeMessage>(new SynchronizeMessage(context.LocalMember, commitIndex), token) : Task.FromResult<long?>(null);

    EndPoint IPeer.EndPoint => EndPoint;

    ClusterMemberId IClusterMember.Id => Id;

    bool IClusterMember.IsLeader => context.IsLeader(this);

    public bool IsRemote => Id != context.LocalMember;

    public ClusterMemberStatus Status
    {
        get => IsRemote ? status.Value : ClusterMemberStatus.Available;
        private set => IClusterMember.OnMemberStatusChanged(this, ref status, value, memberStatusChanged);
    }

    [RequiresPreviewFeatures]
    internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, bool respectLeadership, CancellationToken token)
        => SendAsync<TResponse, CustomMessage<TResponse>>(new(context.LocalMember, message, responseReader) { RespectLeadership = respectLeadership }, token);

    [RequiresPreviewFeatures]
    Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
        => SendMessageAsync(message, responseReader, false, token);

    [RequiresPreviewFeatures]
    internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, bool respectLeadership, CancellationToken token)
        where TResponse : notnull, ISerializable<TResponse>
        => SendAsync<TResponse, CustomSerializableMessage<TResponse>>(new(context.LocalMember, message) { RespectLeadership = respectLeadership }, token);

    [RequiresPreviewFeatures]
    Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, CancellationToken token)
        => SendMessageAsync<TResponse>(message, false, token);

    [RequiresPreviewFeatures]
    internal Task SendSignalAsync(CustomMessage message, CancellationToken token) =>
        SendAsync<IMessage?, CustomMessage>(message, token);

    [RequiresPreviewFeatures]
    Task ISubscriber.SendSignalAsync(IMessage message, bool requiresConfirmation, CancellationToken token)
    {
        var request = new CustomMessage(context.LocalMember, message, requiresConfirmation);
        return SendSignalAsync(request, token);
    }

    ref long IRaftClusterMember.NextIndex => ref nextIndex;

    ref long IRaftClusterMember.ConfigurationFingerprint => ref fingerprint;

    public override string? ToString() => BaseAddress?.ToString();
}