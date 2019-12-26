using System;
using System.Collections.Concurrent;
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
    public sealed class DistributedLockProvider : IDistributedLockProvider, IDistributedServiceProvider
    {
        private IDistributedLockProvider.ConfigurationProvider? lockConfig;
        private readonly IDistributedLockEngine engine;
        private readonly IMessageBus messageBus;
        private readonly ConcurrentDictionary<string, AsyncExclusiveLock> locks;

        private DistributedLockProvider(IDistributedLockEngine engine, IMessageBus messageBus)
        {
            this.engine = engine;
            this.messageBus = messageBus;
            locks = new ConcurrentDictionary<string, AsyncExclusiveLock>(StringComparer.Ordinal);
        }

        private Task<Action?> TryAcquireLockAsync(string lockName, TimeSpan timeout, CancellationToken token)
        {
            if(token.IsCancellationRequested)
                return Task.FromCanceled<Action?>(token);
            timeout.ToString();
            Action release = () => Unlock(lockName, false);
            return Task.FromResult<Action?>(release);
        }

        private void Unlock(string lockName, bool force)
        {
            lockName.ToString();
            force.ToString();
        }

        public Task InitializeAsync(CancellationToken token)
            => engine.RestoreAsync(token);

        public AsyncLock GetLock(string lockName)
            => new AsyncLock((timeout, token) => TryAcquireLockAsync(lockName, timeout, token));
        
        AsyncLock IDistributedLockProvider.this[string lockName] => GetLock(lockName);
        public IDistributedLockProvider.ConfigurationProvider Configuration
        {
            set => lockConfig = value;
        }

        public void ForceUnlock(string lockName)
            => Unlock(lockName, true);

        public static DistributedLockProvider? TryCreate<TCluster>(TCluster cluster)
            where TCluster : class, IMessageBus, IReplicationCluster
        {
            var lockEngine = cluster.AuditTrail as IDistributedLockEngine;
            return lockEngine is null ? null : new DistributedLockProvider(lockEngine, cluster);
        }
    }
}