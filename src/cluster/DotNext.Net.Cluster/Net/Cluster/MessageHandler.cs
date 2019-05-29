using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
    public delegate Task MessageHandler(IClusterMember sender, IMessage message);
}
