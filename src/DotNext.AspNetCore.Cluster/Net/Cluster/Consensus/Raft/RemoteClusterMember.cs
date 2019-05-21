using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RemoteClusterMember : HttpClient, IClusterMember
    {
        internal RemoteClusterMember(Uri remoteMember)
        {
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

        //null means that node is unreachable
        //true means that node votes successfully for the new leader
        //false means that node is in candidate state and rejects voting
        internal Task<bool?> Vote(Guid sender, CancellationToken token)
        {
            
        }

        public IPEndPoint Endpoint { get; }
        public bool IsLeader { get; private set; }

        bool IClusterMember.IsRemote => true;

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public ClusterMemberStatus Status { get; private set; }
    }
}
