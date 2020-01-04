using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IMessage = Messaging.IMessage;

    /// <summary>
    /// Represents distributed service provider.
    /// </summary>
    /// <remarks>
    /// This type is not indendent to be used directly in your code.
    /// </remarks>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class DistributedServiceProvider
    {
        private protected DistributedServiceProvider()
        {

        }

        internal abstract Task RefreshAsync(ISponsor sponsor, CancellationToken token);

        public abstract Task<IMessage> ProcessMessage(IMessage message, CancellationToken token);

        public abstract Task ProcessSignal(IMessage signal, CancellationToken token);

        public abstract Task InitializeAsync(CancellationToken token);
    }
}