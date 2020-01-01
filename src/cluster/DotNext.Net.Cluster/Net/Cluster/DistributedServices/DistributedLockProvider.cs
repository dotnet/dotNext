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
    using static IO.DataTransferObject;
    
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
            DefaultOptions = new IDistributedLockProvider.LockOptions();
        }

        /// <summary>
        /// Gets the default options of the distributed lock.
        /// </summary>
        /// <value>The default lock options.</value>
        public IDistributedLockProvider.LockOptions DefaultOptions { get; }

        private async Task<IMessage> AcquireLockAsync(IMessage message, CancellationToken token)
        {
            var request = await message.GetObjectDataAsync<AcquireLockRequest, IMessage>(token).ConfigureAwait(false);
            return new AcquireLockResponse 
            { 
                Content = await engine.TryAcquireAsync(request.LockName, request.LockInfo, token).ConfigureAwait(false)
            };
        }   

        /// <summary>
        /// Handles service message related to distributed lock management.
        /// </summary>
        /// <remarks>
        /// Ensure that <see cref="IsMessageSupported"/> returned true before
        /// calling this method.
        /// </remarks>
        /// <param name="message">The message to process.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The response message produced by this provider.</returns>
        /// <exception cref="NotSupportedException">The specified message is not supported.</exception>
        public Task<IMessage> ProcessMessage(IMessage message, CancellationToken token)
        {
            switch(message.Name)
            {
                case AcquireLockRequest.Name:
                    return AcquireLockAsync(message, token);
                default:
                    return Task.FromException<IMessage>(new NotSupportedException());
            }
        }

        /// <summary>
        /// Determines whether the specified message is a service message for distributed lock management.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool IsMessageSupported(IMessage message) 
            => message.Name.IsOneOf(AcquireLockRequest.Name);

        private async Task<Action?> TryAcquireLockAsync(string lockName, Timeout timeout, CancellationToken token)
        {
            var options = lockConfig?.Invoke(lockName) ?? DefaultOptions;
            //send Acquire message to leader node
            //if leader doesn't confirm that the lock is acquired then wait for Release log entry
            //in local audit trail and then try again
            var timeoutSource = new TimeoutTokenSource(timeout, token);
            var eventListener = engine.CreateReleaseLockListener(timeoutSource.Token);
            var lockId = Guid.NewGuid();
            try
            {
                for(var request = new AcquireLockRequest { LockName = lockName }; ; await eventListener.WaitAsync().ConfigureAwait(false))
                {
                    request.LockInfo = new DistributedLockInfo
                    {
                        Owner = engine.NodeId,
                        Id = lockId,
                        CreationTime = DateTimeOffset.Now,
                        LeaseTime = options.LeaseTime
                    };
                    if(await messageBus.SendMessageToLeaderAsync(request, AcquireLockResponse.Reader, timeoutSource.Token).ConfigureAwait(false))
                        break;
                }
            }
            catch(OperationCanceledException) when(timeout.IsExpired)    //timeout detected
            {
                return null;
            }
            finally
            {
                await eventListener.ConfigureAwait(false).DisposeAsync();
                timeoutSource.Dispose();
            }
            //acquisition confirmed by leader node so need to wait until the acquire command will be committed
            timeoutSource = new TimeoutTokenSource(timeout, token);
            eventListener = engine.CreateAcquireLockListener(timeoutSource.Token);
            try
            {
                while(!engine.IsAcquired(lockName, lockId))
                    await eventListener.WaitAsync().ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(timeout.IsExpired)    //timeout detected
            {
                return null;
            }
            finally
            {
                await eventListener.ConfigureAwait(false).DisposeAsync();
                timeoutSource.Dispose();
            }
            //finally check the timeout
            return timeout.RemainingTime.TryGetValue(out var remainingTime) ?
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