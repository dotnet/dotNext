using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Messaging;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Replication;
    using Threading;

    internal sealed class RaftClusterMember : HttpClient, IRaftClusterMember, IMessenger
    {
        private const string UserAgent = "Raft.NET";

        private const int UnknownStatus = (int) ClusterMemberStatus.Unknown;
        private const int UnavailableStatus = (int) ClusterMemberStatus.Unavailable;
        private const int AvailableStatus = (int) ClusterMemberStatus.Available;

        private delegate Task<T> ResponseParser<T>(HttpResponseMessage response);

        private readonly Uri resourcePath;
        private int status;
        private readonly IHostingContext context;
        private volatile MemberMetadata metadata;

        internal RaftClusterMember(IHostingContext context, Uri remoteMember, Uri resourcePath)
        {
            this.resourcePath = resourcePath;
            this.context = context;
            status = UnknownStatus;
            BaseAddress = remoteMember;
            Endpoint = remoteMember.ToEndPoint() ?? throw new UriFormatException(ExceptionMessages.UnresolvedHostName(remoteMember.Host));
            DefaultRequestHeaders.ConnectionClose = true;   //to avoid network storm
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, GetType().Assembly.GetName().Version.ToString()));
        }

        private void ChangeStatus(int newState)
        {
            var previousState = status.GetAndSet(newState);
            if(previousState != newState)
                context.MemberStatusChanged(this, (ClusterMemberStatus) previousState, (ClusterMemberStatus) newState);
        }

        private async Task<T> SendAsync<T>(RaftHttpMessage message, ResponseParser<T> parser, CancellationToken token)
        {
            var request = (HttpRequestMessage) message;
            request.RequestUri = resourcePath;
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                    .ConfigureAwait(false);
                ChangeStatus(AvailableStatus);
                return await parser(response).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                ChangeStatus(UnavailableStatus);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                response?.Dispose();
                request.Dispose();
            }
        }

        public async Task HeartbeatAsync(CancellationToken token)
        {
            if(this.Represents(context.LocalEndpoint))
                return;
            var request = (HttpRequestMessage) new HeartbeatMessage(context.LocalEndpoint);
            request.RequestUri = resourcePath;
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
                ChangeStatus(AvailableStatus);
            }
            catch (HttpRequestException)
            {
                ChangeStatus(UnavailableStatus);
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
        public async Task<bool?> VoteAsync(LogEntryId? lastEntry, CancellationToken token)
            => this.Represents(context.LocalEndpoint)
                ? true
                : await SendAsync(new RequestVoteMessage(context.LocalEndpoint, lastEntry), RequestVoteMessage.GetResponse, token)
                    .ContinueWith(task => default(bool?), token, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Current)
                    .ConfigureAwait(false);

        Task<bool> IClusterMember.ResignAsync(CancellationToken token) 
            => SendAsync(new ResignMessage(context.LocalEndpoint), ResignMessage.GetResponse, token);

        public Task<bool> AppendEntriesAsync(ILogEntry<LogEntryId> newEntry, LogEntryId precedingEntry, CancellationToken token)
            => this.Represents(context.LocalEndpoint) ? 
                context.LocalCommitAsync(newEntry) : 
                SendAsync(new AppendEntriesMessage(context.LocalEndpoint, newEntry, precedingEntry), AppendEntriesMessage.GetResponse, token);

        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadata(bool refresh,
            CancellationToken token)
        {
            if (this.Represents(context.LocalEndpoint))
                return context.Metadata;
            if (metadata is null || refresh)
                metadata = await SendAsync(new MetadataMessage(context.LocalEndpoint), MetadataMessage.GetResponse, token).ConfigureAwait(false);
            return metadata;
        }

        public IPEndPoint Endpoint { get; }
        bool IClusterMember.IsLeader => context.IsLeader(this);

        bool IClusterMember.IsRemote => !this.Represents(Endpoint);

        ClusterMemberStatus IClusterMember.Status => (ClusterMemberStatus) status.VolatileRead();

        bool IEquatable<IClusterMember>.Equals(IClusterMember other) => Endpoint.Equals(other?.Endpoint);

        Task<IMessage> IMessenger.SendMessageAsync(IMessage message, CancellationToken token)
            => SendAsync(new CustomMessage(context.LocalEndpoint, message, false),
                CustomMessage.GetResponse, token);

        private static HttpResponseMessage SuppressError(Task task) => null;

        private async void SendUnreliableSignalAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ContinueWith(SuppressError, token, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnRanToCompletion, TaskScheduler.Current)
                .ConfigureAwait(false);
            Disposable.Dispose(request, response);
        }

        async Task IMessenger.SendSignalAsync(IMessage message, bool requiresConfirmation, CancellationToken token)
        {
            var request = (HttpRequestMessage)new CustomMessage(context.LocalEndpoint, message, true);
            request.RequestUri = resourcePath;
            if (requiresConfirmation)
            {
                var response = default(HttpResponseMessage);
                try
                {
                    response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                        .ConfigureAwait(false);
                    ChangeStatus(AvailableStatus);
                    if (response.StatusCode == HttpStatusCode.NotImplemented)
                        throw new NotSupportedException(ExceptionMessages.MessagingNotSupported);
                }
                catch (HttpRequestException e)
                {
                    ChangeStatus(UnavailableStatus);
                    throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
                }
                finally
                {
                    response?.Dispose();
                    request.Dispose();
                }
            }
            else
                SendUnreliableSignalAsync(request, token);
        }
    }
}
