using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftClusterMember : HttpClient, IClusterMember
    {
        private readonly Guid owner;

        internal RaftClusterMember(Guid owner, Uri remoteMember)
        {
            this.owner = owner;
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

        //null means that node is unreachable
        //true means that node votes successfully for the new leader
        //false means that node is in candidate state and rejects voting
        internal async Task<bool?> Vote(CancellationToken token)
        {
            if(IsLocal)
                return true;
            return null;
        }

        public IPEndPoint Endpoint { get; }
        public bool IsLeader { get; private set; }

        bool IClusterMember.IsRemote => !IsLocal;

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public ClusterMemberStatus Status { get; private set; }
    }
}
