using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Membership;
    using Messaging;
    using Threading;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;
    using Timestamp = Diagnostics.Timestamp;

    internal sealed class RaftClusterMember : HttpClient, IRaftClusterMember, ISubscriber
    {
        private const string UserAgent = "Raft.NET";

        private readonly Uri resourcePath;
        private readonly IHostingContext context;
        internal readonly ClusterMemberId Id;
        internal readonly HttpEndPoint EndPoint;
        private AtomicEnum<ClusterMemberStatus> status;
        private volatile MemberMetadata? metadata;
        private Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>>? memberStatusChanged;
        private long nextIndex, fingerprint;
        internal IClientMetricsCollector? Metrics;

        internal RaftClusterMember(IHostingContext context, HttpEndPoint remoteMember, Uri resourcePath, ClusterMemberId? id)
            : base(context.CreateHttpHandler(), true)
        {
            this.resourcePath = resourcePath;
            this.context = context;
            status = new AtomicEnum<ClusterMemberStatus>(ClusterMemberStatus.Unknown);
            EndPoint = remoteMember;
            BaseAddress = remoteMember.CreateUriBuilder().Uri;
            Id = id ?? ClusterMemberId.FromEndPoint(remoteMember);
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, (GetType().Assembly.GetName().Version ?? new Version()).ToString()));
            IsRemote = true;
        }

        event Action<ClusterMemberStatusChangedEventArgs> IClusterMember.MemberStatusChanged
        {
            add => memberStatusChanged += value;
            remove => memberStatusChanged -= value;
        }

        private void ChangeStatus(ClusterMemberStatus newState)
            => IClusterMember.OnMemberStatusChanged(this, ref status, newState, memberStatusChanged);

        internal void Touch() => ChangeStatus(ClusterMemberStatus.Available);

        private async Task<TResult> SendAsync<TResult, TMessage>(TMessage message, CancellationToken token)
            where TMessage : HttpMessage, IHttpMessageReader<TResult>
        {
            context.Logger.SendingRequestToMember(EndPoint, message.MessageType);
            var request = new HttpRequestMessage
            {
                RequestUri = resourcePath,
                Version = DefaultRequestVersion,
                VersionPolicy = DefaultVersionPolicy,
            };

            message.PrepareRequest(request);

            // setup additional timeout control token needed if actual timeout
            // doesn't match to HttpClient.Timeout
            CancellationTokenSource? timeoutControl;
            CancellationToken tokenWithTimeout;
            if (context.TryGetTimeout<TMessage>(out var timeout) && timeout != Timeout)
            {
                timeoutControl = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutControl.CancelAfter(timeout);
                tokenWithTimeout = token;
            }
            else
            {
                timeoutControl = null;
                tokenWithTimeout = token;
            }

            // do HTTP request and use token associated with custom timeout
            var response = default(HttpResponseMessage);
            var timeStamp = Timestamp.Current;
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, tokenWithTimeout)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                ChangeStatus(ClusterMemberStatus.Available);
                return await message.ParseResponse(response, tokenWithTimeout).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                if (response is null || message.IsMemberUnavailable(e.StatusCode))
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
            catch (OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                // This handler catches inability to connect to the remote host on Windows platform.
                // On Linux, this situation is handled by handler for HttpRequestException
                throw MemberUnavailable(e);
            }
            finally
            {
                timeoutControl?.Dispose();
                Disposable.Dispose(response, response?.Content, request);
                Metrics?.ReportResponseTime(timeStamp.Elapsed);
            }
        }

        private MemberUnavailableException MemberUnavailable(Exception e)
        {
            context.Logger.MemberUnavailable(EndPoint, e);
            ChangeStatus(ClusterMemberStatus.Unavailable);
            return new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
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
            => Id == context.LocalMember
                ? Task.FromResult(new Result<bool>(term, true))
                : SendAsync<Result<bool>, RequestVoteMessage>(new RequestVoteMessage(context.LocalMember, term, lastLogIndex, lastLogTerm), token);

        Task<Result<bool>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => Id == context.LocalMember
                ? Task.FromResult(new Result<bool>(term, true))
                : SendAsync<Result<bool>, PreVoteMessage>(new PreVoteMessage(context.LocalMember, term, lastLogIndex, lastLogTerm), token);

        Task<bool> IClusterMember.ResignAsync(CancellationToken token)
            => SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalMember), token);

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
            if (Id == context.LocalMember)
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, AppendEntriesMessage<TEntry, TList>>(new AppendEntriesMessage<TEntry, TList>(context.LocalMember, term, prevLogIndex, prevLogTerm, commitIndex, entries, configuration, applyConfig) { UseOptimizedTransfer = context.UseEfficientTransferOfLogEntries }, token);
        }

        Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        {
            if (Id == context.LocalMember)
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, InstallSnapshotMessage>(new InstallSnapshotMessage(context.LocalMember, term, snapshotIndex, snapshot), token);
        }

        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
        {
            if (Id == context.LocalMember)
                return context.Metadata;
            if (metadata is null || refresh)
                metadata = await SendAsync<MemberMetadata, MetadataMessage>(new MetadataMessage(context.LocalMember), token).ConfigureAwait(false);
            return metadata;
        }

        EndPoint IPeer.EndPoint => EndPoint;

        ClusterMemberId IClusterMember.Id => Id;

        bool IClusterMember.IsLeader => context.IsLeader(this);

        public bool IsRemote
        {
            get;
            internal set;
        }

        ClusterMemberStatus IClusterMember.Status
            => Id == context.LocalMember ? ClusterMemberStatus.Available : status.Value;

        internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, bool respectLeadership, CancellationToken token)
            => SendAsync<TResponse, CustomMessage<TResponse>>(new CustomMessage<TResponse>(context.LocalMember, message, responseReader) { RespectLeadership = respectLeadership }, token);

        Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
            => SendMessageAsync(message, responseReader, false, token);

        internal Task SendSignalAsync(CustomMessage message, CancellationToken token) =>
            SendAsync<IMessage?, CustomMessage>(message, token);

        Task ISubscriber.SendSignalAsync(IMessage message, bool requiresConfirmation, CancellationToken token)
        {
            var request = new CustomMessage(context.LocalMember, message, requiresConfirmation);
            return SendSignalAsync(request, token);
        }

        ref long IRaftClusterMember.NextIndex => ref nextIndex;

        ref long IRaftClusterMember.ConfigurationFingerprint => ref fingerprint;

        public override string? ToString() => BaseAddress?.ToString();
    }
}
