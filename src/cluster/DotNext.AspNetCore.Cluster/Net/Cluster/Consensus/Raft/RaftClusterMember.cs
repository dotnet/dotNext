using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftClusterMember : HttpClient, IClusterMember
    {
        private const int UnknownStatus = (int) ClusterMemberStatus.Unknown;
        private const int UnavailableStatus = (int) ClusterMemberStatus.Unavailable;
        private const int AvailableStatus = (int) ClusterMemberStatus.Available;

        private readonly Uri resourcePath;
        private int status;

        internal RaftClusterMember(Uri remoteMember, Uri resourcePath)
        {
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

        private void ChangeStatus(int newState, ClusterMemberStatusChanged callback)
        {
            var previousState = status.GetAndSet(newState);
            callback?.Invoke(this, (ClusterMemberStatus) previousState, (ClusterMemberStatus) newState);
        }

        //null means that node is unreachable
        //true means that node votes successfully for the new leader
        //false means that node is in candidate state and rejects voting
        internal async Task<bool?> Vote(ILocalMember sender, ClusterMemberStatusChanged callback, CancellationToken token)
        {
            if (IsLocal)
                return true;
            var request = (HttpRequestMessage) new RequestVoteMessage(sender.Id, sender.Term);
            var response = default(HttpResponseMessage);
            try
            {
                response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                    .ConfigureAwait(false);
                ChangeStatus(AvailableStatus, callback);
                var reply = await RequestVoteMessage.GetResponse(response).ConfigureAwait(false);
                IsLocal = reply.MemberId == sender.Id;
                Id = reply.MemberId;
                Name = reply.MemberName;
                return reply.Body;
            }
            catch (HttpRequestException)
            {
                ChangeStatus(UnavailableStatus, callback);
                return null;
            }
            finally
            {
                response?.Dispose();
                request.Dispose();
            }
        }

        public IPEndPoint Endpoint { get; }
        public bool IsLeader { get; private set; }

        bool IClusterMember.IsRemote => !IsLocal;

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        ClusterMemberStatus IClusterMember.Status => (ClusterMemberStatus) status.VolatileRead();
    }
}
