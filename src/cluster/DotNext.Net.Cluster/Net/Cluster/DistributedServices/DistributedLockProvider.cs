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
    using Timestamp = Diagnostics.Timestamp;
    
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
        private readonly IDistributedLockProvider.LockOptions defaultOptions;

        private DistributedLockProvider(IDistributedLockEngine engine, IMessageBus messageBus)
        {
            this.engine = engine;
            this.messageBus = messageBus;
            locks = new ConcurrentDictionary<string, AsyncExclusiveLock>(StringComparer.Ordinal);
            defaultOptions = new IDistributedLockProvider.LockOptions();
        }

        private async Task<Action?> TryAcquireLockAsync(string lockName, Timeout timeout, CancellationToken token)
        {
            var options = lockConfig?.Invoke(lockName) ?? defaultOptions;
            //send Acquire message to leader node
            //if leader doesn't confirm that the lock is acquired then wait for Release log entry
            //in local audit trail and then try again
            using(var timeoutToken = new CancellationTokenSource(timeout))
            using(var linkedToken = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(timeoutToken.Token, token) : timeoutToken)
            await using(var releaseListener = engine.CreateReleaseLockListener(linkedToken.Token))
            {
            }
            //acquisition confirmed by leader node so just wait for replication of audit trail containing
            //confirmation of lock acquisition
            return timeout.RemainingTime.TryGetValue(out var remainingTime) && await engine.WaitForAcquisitionAsync(lockName, remainingTime, token).ConfigureAwait(false) ?
                new Action(() => Unlock(lockName, false)) :
                null;
        }

        private void Unlock(string lockName, bool force)
        {
            lockName.ToString();
            force.ToString();
        }

        public Task InitializeAsync(CancellationToken token)
            => engine.RestoreAsync(token);

        public AsyncLock GetLock(string lockName)
            => new AsyncLock((timeout, token) => TryAcquireLockAsync(lockName, new Timeout(timeout), token));
        
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