using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftClusterMember : HttpClient, IRaftClusterMember
    {
        private delegate Task<RaftHttpMessage<TBody>.Response> ResponseParser<TBody>(HttpResponseMessage response);

        private const int UnknownStatus = (int) ClusterMemberStatus.Unknown;
        private const int UnavailableStatus = (int) ClusterMemberStatus.Unavailable;
        private const int AvailableStatus = (int) ClusterMemberStatus.Available;

        private readonly Uri resourcePath;
        private int status;
        private readonly IRaftLocalMember owner;

        internal RaftClusterMember(IRaftLocalMember owner, Uri remoteMember, Uri resourcePath)
        {
            this.owner = owner;
            status = UnknownStatus;
            this.resourcePath = resourcePath;
            BaseAddress = remoteMember;
            switch (remoteMember.HostNameType)
            {
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    Endpoint = new IPEndPoint(IPAddress.Parse(remoteMember.Host), remoteMember.Port);
                    break;
                case UriHostNameType.Dns:
                    var entry = Dns.GetHostEntry(remoteMember.Host);
                    Endpoint = new IPEndPoint(entry.AddressList[0], remoteMember.Port);
                    break;
                default:
                    throw new UriFormatException(ExceptionMessages.UnresolvedHostName(remoteMember.Host));
            }
        }

        internal bool IsLocal { get; private set; }

        private void ChangeStatus(in RequestContext context, int newState)
        {
            var previousState = status.GetAndSet(newState);
            if(previousState != newState)
                context.MemberStatusChanged(this, (ClusterMemberStatus) previousState, (ClusterMemberStatus) newState);
        }

        private async Task<TBody> ParseResponse<TBody>(HttpResponseMessage response, ResponseParser<TBody> parser)
        {
            var reply = await parser(response).ConfigureAwait(false);
            IsLocal = reply.MemberId == owner.Id;
            Id = reply.MemberId;
            if(!string.Equals(Name, reply.MemberName))  //allows not to store the same string value but represented by different instances
                Name = reply.MemberName;
            return reply.Body;
        }

        //null means that node is unreachable
        //true means that node votes successfully for the new leader
        //false means that node is in candidate state and rejects voting
        public async Task<bool?> Vote(RequestContext context, CancellationToken token)
        {
            if (IsLocal)
                return true;
            var request = (HttpRequestMessage) new RequestVoteMessage(owner);
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
                var result = await ParseResponse<bool>(response, RequestVoteMessage.GetResponse).ConfigureAwait(false);
                ChangeStatus(context, AvailableStatus);
                return result;
            }
            catch (HttpRequestException)
            {
                ChangeStatus(context, UnavailableStatus);
                return null;
            }
            finally
            {
                response?.Dispose();
                request.Dispose();
            }
        }

        public async Task<bool> Resign(RequestContext context, CancellationToken token)
        {
            var request = (HttpRequestMessage) new ResignMessage(owner);
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                    .ConfigureAwait(false);
                var result = await ParseResponse<bool>(response, ResignMessage.GetResponse).ConfigureAwait(false);
                ChangeStatus(context, AvailableStatus);
                return result;
            }
            catch (HttpRequestException)
            {
                ChangeStatus(context, UnavailableStatus);
                return false;
            }
            finally
            {
                response?.Dispose();
                request.Dispose();
            }
        }

        public IPEndPoint Endpoint { get; }
        bool IClusterMember.IsLeader => owner.IsLeader(this);

        bool IClusterMember.IsRemote => !IsLocal;

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        ClusterMemberStatus IClusterMember.Status => (ClusterMemberStatus) status.VolatileRead();
    }
}
