using System;
using System.Net;
using System.Net.Http;

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

        internal void Vote()
        {

        }

        public IPEndPoint Endpoint { get; }
        public bool IsLeader { get; private set; }

        bool IClusterMember.IsRemote => true;

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public bool IsAvailable { get; private set; }

        internal void UpdateStatus(Guid id, string name, bool available)
        {
            Id = id;
            Name = name;
            IsAvailable = available;
        }
    }
}
