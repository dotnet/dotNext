using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Generic;
    using Messaging;
    using Replication;
    using Threading;
    using Threading.Tasks;

    internal sealed class RaftClusterMember : HttpClient, IRaftClusterMember, IAddressee
    {
        private const string UserAgent = "Raft.NET";

        private const int UnknownStatus = (int)ClusterMemberStatus.Unknown;
        private const int UnavailableStatus = (int)ClusterMemberStatus.Unavailable;
        private const int AvailableStatus = (int)ClusterMemberStatus.Available;

        private readonly Uri resourcePath;
        private int status;
        private readonly IHostingContext context;
        private volatile MemberMetadata metadata;
        private ClusterMemberStatusChanged memberStatusChanged;

        internal RaftClusterMember(IHostingContext context, Uri remoteMember, Uri resourcePath)
            : base(context.CreateHttpHandler(), true)
        {
            this.resourcePath = resourcePath;
            this.context = context;
            status = UnknownStatus;
            BaseAddress = remoteMember;
            Endpoint = remoteMember.ToEndPoint() ?? throw new UriFormatException(ExceptionMessages.UnresolvedHostName(remoteMember.Host));
            DefaultRequestHeaders.ConnectionClose = true;   //to avoid network storm
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
                memberStatusChanged?.Invoke(this, (ClusterMemberStatus) previousState, (ClusterMemberStatus) newState);
        }

        internal void Touch() => ChangeStatus(AvailableStatus);

        private async Task<R> SendAsync<R, M>(M message, CancellationToken token)
            where M : HttpMessage, IHttpMessageReader<R>
        {
            context.Logger.SendingRequestToMember(Endpoint, message.MessageType);
            var request = (HttpRequestMessage)message;
            request.RequestUri = resourcePath;
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false);
                ChangeStatus(AvailableStatus);
                response.EnsureSuccessStatusCode();
                return await message.ParseResponse(response).ConfigureAwait(false);
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
            catch(OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                context.Logger.MemberUnavailable(Endpoint, e);
                ChangeStatus(UnavailableStatus);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                response?.Dispose();
                request.Dispose();
            }
        }

        async Task IRaftClusterMember.HeartbeatAsync(long term, CancellationToken token)
        {
            if (Endpoint.Equals(context.LocalEndpoint))
                return;
            context.Logger.SendingRequestToMember(Endpoint, HeartbeatMessage.MessageType);
            var request = (HttpRequestMessage)new HeartbeatMessage(context.LocalEndpoint, term);
            request.RequestUri = resourcePath;
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                ChangeStatus(AvailableStatus);
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
            catch(OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                context.Logger.MemberUnavailable(Endpoint, e);
                ChangeStatus(UnavailableStatus);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                response?.Dispose();
                request.Dispose();
            }
        }

        //null means that node is unreachable
        //true means that node votes successfully for the new leader
        //false means that node is in candidate state and rejects voting
        async Task<bool?> IRaftClusterMember.VoteAsync(long term, LogEntryId? lastEntry, CancellationToken token)
            => Endpoint.Equals(context.LocalEndpoint)
                ? true
                : await SendAsync<bool, RequestVoteMessage>(new RequestVoteMessage(context.LocalEndpoint, term, lastEntry), token)
                    .ToNullable()
                    .OnFaulted<bool?, DefaultConst<bool?>>()
                    .ConfigureAwait(false);

        Task<bool> IClusterMember.ResignAsync(CancellationToken token)
            => SendAsync<bool, ResignMessage>(new ResignMessage(context.LocalEndpoint), token);

        Task<bool> IRaftClusterMember.AppendEntriesAsync(long term, ILogEntry<LogEntryId> newEntry, LogEntryId precedingEntry, CancellationToken token)
            => Endpoint.Equals(context.LocalEndpoint) ?
                context.LocalCommitAsync(newEntry) :
                SendAsync<bool, AppendEntriesMessage>(new AppendEntriesMessage(context.LocalEndpoint, term, newEntry, precedingEntry), token);

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

        Task<TResponse> IAddressee.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token)
            => SendMessageAsync(message, responseReader, false, token);

        private async void SendUnreliableSignalAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .OnFaultedOrCanceled<HttpResponseMessage, DefaultConst<HttpResponseMessage>>()
                .ConfigureAwait(false);
            Disposable.Dispose(request, response);
        }

        internal Task SendSignalAsync(IMessage message, bool requiresConfirmation, bool respectLeadership, CancellationToken token)
        {
            var request = new CustomMessage(context.LocalEndpoint, message, true) { RespectLeadership = respectLeadership };
            if (requiresConfirmation)
                return SendAsync<IMessage, CustomMessage>(request, token);
            else
            {
                SendUnreliableSignalAsync((HttpRequestMessage) request, token);
                return Task.CompletedTask;
            }
        }

        Task IAddressee.SendSignalAsync(IMessage message, bool requiresConfirmation, CancellationToken token)
            => SendSignalAsync(message, requiresConfirmation, false, token);
    }
}
