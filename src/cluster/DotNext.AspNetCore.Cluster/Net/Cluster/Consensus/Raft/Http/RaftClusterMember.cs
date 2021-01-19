﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Threading;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;
    using Timestamp = Diagnostics.Timestamp;

    internal sealed class RaftClusterMember : HttpClient, IRaftClusterMember, ISubscriber
    {
        private const string UserAgent = "Raft.NET";

        private static readonly Version Http1 = new Version(1, 1);
        private static readonly Version Http2 = new Version(2, 0);
        private readonly Uri resourcePath;
        private readonly IHostingContext context;
        private AtomicEnum<ClusterMemberStatus> status;
        private volatile MemberMetadata? metadata;
        private ClusterMemberStatusChanged? memberStatusChanged;
        private long nextIndex;
        internal IClientMetricsCollector? Metrics;
        internal HttpVersion ProtocolVersion;

        internal RaftClusterMember(IHostingContext context, Uri remoteMember, Uri resourcePath)
            : base(context.CreateHttpHandler(), true)
        {
            this.resourcePath = resourcePath;
            this.context = context;
            status = new AtomicEnum<ClusterMemberStatus>(ClusterMemberStatus.Unknown);
            BaseAddress = remoteMember;
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, (GetType().Assembly.GetName().Version ?? new Version()).ToString()));
        }

        event ClusterMemberStatusChanged? IClusterMember.MemberStatusChanged
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
            context.Logger.SendingRequestToMember(Endpoint, message.MessageType);
            var request = new HttpRequestMessage { RequestUri = resourcePath };
            switch (ProtocolVersion)
            {
                case HttpVersion.Http1:
                    request.Version = Http1;
                    break;
                case HttpVersion.Http2:
                    request.Version = Http2;
                    break;
            }

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
                if (response is null)
                {
                    context.Logger.MemberUnavailable(Endpoint, e);
                    ChangeStatus(ClusterMemberStatus.Unavailable);
                    throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
                }

                throw new UnexpectedStatusCodeException(response, e);
            }
            catch (OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                context.Logger.MemberUnavailable(Endpoint, e);
                ChangeStatus(ClusterMemberStatus.Unavailable);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                timeoutControl?.Dispose();
                Disposable.Dispose(response, response?.Content, request);
                Metrics?.ReportResponseTime(timeStamp.Elapsed);
            }
        }

        ValueTask IRaftClusterMember.CancelPendingRequestsAsync()
        {
            CancelPendingRequests();
            return new ValueTask();
        }

        Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => Endpoint.Equals(context.LocalEndpoint)
                ? Task.FromResult(new Result<bool>(term, true))
                : SendAsync<Result<bool>, RequestVoteMessage>(new RequestVoteMessage(context.LocalEndpoint, term, lastLogIndex, lastLogTerm), token);

        Task<bool> IClusterMember.ResignAsync(CancellationToken token)
            => SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalEndpoint), token);

        Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(
            long term,
            TList entries,
            long prevLogIndex,
            long prevLogTerm,
            long commitIndex,
            CancellationToken token)
        {
            if (Endpoint.Equals(context.LocalEndpoint))
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, AppendEntriesMessage<TEntry, TList>>(new AppendEntriesMessage<TEntry, TList>(context.LocalEndpoint, term, prevLogIndex, prevLogTerm, commitIndex, entries), token);
        }

        Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        {
            if (Endpoint.Equals(context.LocalEndpoint))
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, InstallSnapshotMessage>(new InstallSnapshotMessage(context.LocalEndpoint, term, snapshotIndex, snapshot), token);
        }

        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
        {
            if (Endpoint.Equals(context.LocalEndpoint))
                return context.Metadata;
            if (metadata is null || refresh)
                metadata = await SendAsync<MemberMetadata, MetadataMessage>(new MetadataMessage(context.LocalEndpoint), token).ConfigureAwait(false);
            return metadata;
        }

        public IPEndPoint Endpoint =>
            BaseAddress.ToEndPoint() ?? throw new UriFormatException(ExceptionMessages.UnresolvedHostName(BaseAddress.Host));

        bool IClusterMember.IsLeader => context.IsLeader(this);

        public bool IsRemote => !Endpoint.Equals(context.LocalEndpoint);

        ClusterMemberStatus IClusterMember.Status
            => Endpoint.Equals(context.LocalEndpoint) ? ClusterMemberStatus.Available : status.Value;

        internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, bool respectLeadership, CancellationToken token)
            => SendAsync<TResponse, CustomMessage<TResponse>>(new CustomMessage<TResponse>(context.LocalEndpoint, message, responseReader) { RespectLeadership = respectLeadership }, token);

        Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
            => SendMessageAsync(message, responseReader, false, token);

        internal Task SendSignalAsync(CustomMessage message, CancellationToken token) =>
            SendAsync<IMessage, CustomMessage>(message, token);

        Task ISubscriber.SendSignalAsync(IMessage message, bool requiresConfirmation, CancellationToken token)
        {
            var request = new CustomMessage(context.LocalEndpoint, message, requiresConfirmation);
            return SendSignalAsync(request, token);
        }

        ref long IRaftClusterMember.NextIndex => ref nextIndex;

        public override string ToString() => BaseAddress.ToString();
    }
}
