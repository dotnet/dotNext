using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IMessage = Messaging.IMessage;

    public abstract class DistributedServiceProvider
    {
        private protected DistributedServiceProvider()
        {

        }

        public abstract Task<IMessage> ProcessMessage(IMessage message, CancellationToken token);

        public abstract Task ProcessSignal(IMessage signal, CancellationToken token);

        public abstract Task InitializeAsync(CancellationToken token);
    }
}