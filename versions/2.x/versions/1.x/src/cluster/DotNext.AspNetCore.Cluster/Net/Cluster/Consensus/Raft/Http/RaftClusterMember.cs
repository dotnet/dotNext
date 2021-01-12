using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Threading;
    using Timestamp = Diagnostics.Timestamp;

    internal sealed class RaftClusterMember : HttpClient, IRaftClusterMember, ISubscriber
    {
        private const string UserAgent = "Raft.NET";

        private const int UnknownStatus = (int)ClusterMemberStatus.Unknown;
        private const int UnavailableStatus = (int)ClusterMemberStatus.Unavailable;
        private const int AvailableStatus = (int)ClusterMemberStatus.Available;

        private static readonly Version Http1 = new Version(1, 1);
        private static readonly Version Http2 = new Version(2, 0);
        private readonly Uri resourcePath;
        private int status;
        private readonly IHostingContext context;
        private volatile MemberMetadata metadata;
        private ClusterMemberStatusChanged memberStatusChanged;
        private long nextIndex;
        internal IHttpClientMetrics Metrics;
        internal HttpVersion ProtocolVersion;

        internal RaftClusterMember(IHostingContext context, Uri remoteMember, Uri resourcePath)
            : base(context.CreateHttpHandler(), true)
        {
            this.resourcePath = resourcePath;
            this.context = context;
            status = UnknownStatus;
            BaseAddress = remoteMember;
            Endpoint = remoteMember.ToEndPoint() ?? throw new UriFormatException(ExceptionMessages.UnresolvedHostName(remoteMember.Host));
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, GetType().Assembly.GetName().Version.ToString()));
        }

        event ClusterMemberStatusChanged IClusterMember.MemberStatusChanged
        {
            add => memberStatusChanged += value;
            remove => memberStatusChanged -= value;
        }

        private void ChangeStatus(int newState)
        {
            var previousState = status.GetAndSet(newState);
            if (previousState != newState)
                memberStatusChanged?.Invoke(this, (ClusterMemberStatus)previousState, (ClusterMemberStatus)newState);
        }

        internal void Touch() => ChangeStatus(AvailableStatus);

        [SuppressMessage("Reliability", "CA2000", Justification = "Response is disposed in finally block")]
        private async Task<R> SendAsync<R, M>(M message, CancellationToken token)
            where M : HttpMessage, IHttpMessageReader<R>
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

            var response = default(HttpResponseMessage);
            var timeStamp = Timestamp.Current;
            try
            {
                response = (await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false)).EnsureSuccessStatusCode();
                ChangeStatus(AvailableStatus);
                return await message.ParseResponse(response, token).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                if (response is null)
                {
                    context.Logger.MemberUnavailable(Endpoint, e);
                    ChangeStatus(UnavailableStatus);
                    throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
                }
                else
                    throw new UnexpectedStatusCodeException(response, e);
            }
            catch (OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                context.Logger.MemberUnavailable(Endpoint, e);
                ChangeStatus(UnavailableStatus);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                Disposable.Dispose(response, response?.Content, request);
                Metrics?.ReportResponseTime(timeStamp.Elapsed);
            }
        }

        //null means that node is unreachable

        Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => Endpoint.Equals(context.LocalEndpoint)
                ? Task.FromResult(new Result<bool>(term, true))
                : SendAsync<Result<bool>, RequestVoteMessage>(new RequestVoteMessage(context.LocalEndpoint, term, lastLogIndex, lastLogTerm), token);

        Task<bool> IClusterMember.ResignAsync(CancellationToken token)
            => SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalEndpoint), token);

        Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries,
            long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            if (Endpoint.Equals(context.LocalEndpoint))
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, AppendEntriesMessage<TEntry, TList>>(new AppendEntriesMessage<TEntry, TList>(context.LocalEndpoint, term, prevLogIndex, prevLogTerm, commitIndex, entries), token);
        }

        Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
            => SendAsync<Result<bool>, InstallSnapshotMessage>(new InstallSnapshotMessage(context.LocalEndpoint, term, snapshotIndex, snapshot), token);

        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadata(bool refresh,
            CancellationToken token)
        {
            if (Endpoint.Equals(context.LocalEndpoint))
                return context.Metadata;
            if (metadata is null || refresh)
                metadata = await SendAsync<MemberMetadata, MetadataMessage>(new MetadataMessage(context.LocalEndpoint), token).ConfigureAwait(false);
            return metadata;
        }

        public IPEndPoint Endpoint { get; }
        bool IClusterMember.IsLeader => context.IsLeader(this);

        public bool IsRemote => !Endpoint.Equals(context.LocalEndpoint);

        ClusterMemberStatus IClusterMember.Status
            => Endpoint.Equals(context.LocalEndpoint) ? ClusterMemberStatus.Available : (ClusterMemberStatus)status.VolatileRead();

        bool IEquatable<IClusterMember>.Equals(IClusterMember other) => Endpoint.Equals(other?.Endpoint);

        internal Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, bool respectLeadership, CancellationToken token)
            => SendAsync<TResponse, CustomMessage<TResponse>>(new CustomMessage<TResponse>(context.LocalEndpoint, message, responseReader) { RespectLeadership = respectLeadership }, token);

        Task<TResponse> ISubscriber.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
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
