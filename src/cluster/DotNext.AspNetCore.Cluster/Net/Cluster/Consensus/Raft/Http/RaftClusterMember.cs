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
using Threading;
using IClientMetricsCollector = Metrics.IClientMetricsCollector;
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
    private AtomicEnum<ClusterMemberStatus> status;
    private volatile MemberMetadata? metadata;
    private InvocationList<Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>>> memberStatusChanged;
    private long nextIndex, fingerprint;

    [Obsolete("Use System.Diagnostics.Metrics infrastructure instead.")]
    internal IClientMetricsCollector? Metrics;

    static RaftClusterMember()
    {
        var meter = new Meter("DotNext.Net.Cluster.Consensus.Raft.Client");
        ResponseTimeMeter = meter.CreateHistogram<double>("response-time", unit: "ms", description: "Response Time");
    }

    internal RaftClusterMember(IHostingContext context, UriEndPoint remoteMember)
        : base(remoteMember.Uri, context.CreateHttpHandler(), true)
    {
        this.context = context;
        status = new(ClusterMemberStatus.Unknown);
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
#pragma warning disable CA2252
        context.Logger.SendingRequestToMember(EndPoint, TMessage.MessageType);
#pragma warning restore CA2252
        var request = new HttpRequestMessage
        {
            RequestUri = resourcePath,
            Version = DefaultRequestVersion,
            VersionPolicy = DefaultVersionPolicy,
        };

#pragma warning disable CA2252
        HttpMessage.SetMessageType<TMessage>(request);
#pragma warning restore CA2252
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
#pragma warning disable CA2252
            if (response is null || TMessage.IsMemberUnavailable(e.StatusCode))
#pragma warning restore CA2252
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
#pragma warning disable CS0618
            Metrics?.ReportResponseTime(TimeSpan.FromMilliseconds(responseTime));
#pragma warning restore CS0618
            ResponseTimeMeter.Record(
                responseTime,
#pragma warning disable CA2252
                new(IRaftClusterMember.MessageTypeAttributeName, TMessage.MessageType),
#pragma warning restore CA2252
                cachedRemoteAddressAttribute);
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

    Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Result<bool>>(token)
            : IsRemote
            ? SendAsync<Result<bool>, RequestVoteMessage>(new RequestVoteMessage(context.LocalMemberId, term, lastLogIndex, lastLogTerm), token)
            : Task.FromResult(new Result<bool>(term, true));
    }

    Task<Result<PreVoteResult>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Result<PreVoteResult>>(token)
            : IsRemote
            ? SendAsync<Result<PreVoteResult>, PreVoteMessage>(new PreVoteMessage(context.LocalMemberId, term, lastLogIndex, lastLogTerm), token)
            : Task.FromResult(new Result<PreVoteResult>(term, PreVoteResult.Accepted));
    }

    Task<bool> IClusterMember.ResignAsync(CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<bool>(token)
            : SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalMemberId), token);
    }

    Task<Result<bool?>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(
        long term,
        TList entries,
        long prevLogIndex,
        long prevLogTerm,
        long commitIndex,
        IClusterConfiguration configuration,
        bool applyConfig,
        CancellationToken token)
    {
        Task<Result<bool?>> result;

        if (token.IsCancellationRequested)
        {
            result = Task.FromCanceled<Result<bool?>>(token);
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
                result = Task.FromException<Result<bool?>>(e);
                goto exit;
            }

            result = SendAsync<Result<bool?>, AppendEntriesMessage<TEntry, TList>>(message, token);
        }
        else
        {
            result = Task.FromResult(new Result<bool?>(term, value: true));
        }

    exit:
        return result;
    }

    Task<Result<bool?>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
    {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Result<bool?>>(token)
            : IsRemote
            ? SendAsync<Result<bool?>, InstallSnapshotMessage>(new InstallSnapshotMessage(context.LocalMemberId, term, snapshotIndex, snapshot), token)
            : Task.FromResult(new Result<bool?>(term, value: true));
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
        get => IsRemote ? status.Value : ClusterMemberStatus.Available;
        private set => IClusterMember.OnMemberStatusChanged(this, ref status, value, memberStatusChanged);
    }

    internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, bool respectLeadership, CancellationToken token)
        => SendAsync<TResponse, CustomMessage<TResponse>>(new(context.LocalMemberId, message, responseReader) { RespectLeadership = respectLeadership }, token);

    Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
        => SendMessageAsync(message, responseReader, false, token);

    internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, bool respectLeadership, CancellationToken token)
        where TResponse : notnull, ISerializable<TResponse>
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

    ref long IRaftClusterMember.NextIndex => ref nextIndex;

    ref long IRaftClusterMember.ConfigurationFingerprint => ref fingerprint;

    public override string? ToString() => BaseAddress?.ToString();
}