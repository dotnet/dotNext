using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using Messaging;
    using Replication;
    using Threading;
    
    /// <summary>
    /// Represents default implementation of distributed lock provider.
    /// </summary>
    /// <remarks>
    /// This type is not indendent to be used directly in your code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public sealed class DistributedLockProvider : IDistributedLockProvider
    {
        private IDistributedLockProvider.ConfigurationProvider? lockConfig;
        private readonly IDistributedLockEngine engine;
        private readonly IMessageBus messageBus;

        private DistributedLockProvider(IDistributedLockEngine engine, IMessageBus messageBus)
        {
            this.engine = engine;
            this.messageBus = messageBus;
        }

        private Task<Action?> TryAcquireLockAsync(string lockName, TimeSpan timeout, CancellationToken token)
        {
            Action release = () => Unlock(lockName);
            return Task.FromResult<Action?>(release);
        }

        private void Unlock(string lockName)
        {
        }

        public AsyncLock GetLock(string lockName)
            => new AsyncLock((timeout, token) => TryAcquireLockAsync(lockName, timeout, token));
        
        public IDistributedLockProvider.ConfigurationProvider LockConfiguration
        {
            set => lockConfig = value;
        }

        AsyncLock IDistributedLockProvider.this[string lockName] => GetLock(lockName);
        IDistributedLockProvider.ConfigurationProvider IDistributedLockProvider.Configuration
        {
            set => LockConfiguration = value;
        }

        public Task ForceUnlockAsync(string lockName, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public static DistributedLockProvider? TryCreate<TCluster>(TCluster cluster)
            where TCluster : class, IMessageBus, IReplicationCluster
        {
            var lockEngine = cluster.AuditTrail as IDistributedLockEngine;
            return lockEngine is null ? null : new DistributedLockProvider(lockEngine, cluster);
        }
    }
}