using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using Messaging;

    public delegate Task ReplicationEventHandler(IClusterMember sender, IMessage message);
}