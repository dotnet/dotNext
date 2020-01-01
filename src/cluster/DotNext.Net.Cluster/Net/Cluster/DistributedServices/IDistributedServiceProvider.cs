using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IMessage = Messaging.IMessage;

    internal interface IDistributedServiceProvider
    {
        Task<IMessage> ProcessMessage(IMessage message, CancellationToken token);

        Task InitializeAsync(CancellationToken token);
    }
}