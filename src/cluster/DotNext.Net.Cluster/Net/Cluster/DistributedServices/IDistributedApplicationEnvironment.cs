using System;

namespace DotNext.Net.Cluster.DistributedServices
{
    using Messaging;
    using Replication;
    using DistributedServiceProviderAttribute = Runtime.CompilerServices.DistributedServiceProviderAttribute;
    using IDistributedLockProvider = Threading.IDistributedLockProvider;

    /// <summary>
    /// Represents environment of distributed application.
    /// </summary>
    public interface IDistributedApplicationEnvironment : IReplicationCluster, IMessageBus
    {
        /// <summary>
        /// Gets distributed lock provider.
        /// </summary>
        /// <value>The lock provider.</value>
        /// <exception cref="NotSupportedException">The distributed service is not supported.</exception>
        [DistributedServiceProvider]
        IDistributedLockProvider LockProvider { get; }
    }
}