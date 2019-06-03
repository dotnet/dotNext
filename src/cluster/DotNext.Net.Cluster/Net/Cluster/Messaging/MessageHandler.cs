using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    public delegate Task MessageHandler(IClusterMember sender, IMessage message);
}
