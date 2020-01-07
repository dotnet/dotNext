using System;
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
    public sealed class DistributedLockProvider : IDistributedLockProvider, IMessageHandler
    {
        /*
         * Lock acquisition algorithm:
         * 1. Send AcquireLock message to leader node. It is non-blocking RPC call
         * 2. Leader node checks whether the lock is not acquired. If so, respond with `true` and append acquisition command to audit trail for replication. Otherwise, immediately return `false`
         * 3. Requester waits until leader responded with `true`. Timeout control is implemented at this step
         * 4. If requester receives `true` then wait for the commit of the lock acquisition at its local audit trail 
         */

        private DistributedLockConfigurationProvider? lockConfig;
        private readonly IDistributedLockEngine engine;
        private readonly IMessageBus messageBus;
        private ClusterMemberId owner;

        private DistributedLockProvider(IDistributedLockEngine engine, IMessageBus messageBus)
        {
            this.engine = engine;
            this.messageBus = messageBus;
            DefaultOptions = new DistributedLockOptions();
        }

        /// <summary>
        /// Gets the default options of the distributed lock.
        /// </summary>
        /// <value>The default lock options.</value>
        public DistributedLockOptions DefaultOptions { get; }

        private async Task<IMessage> AcquireLockAsync(IMessage message, CancellationToken token)
        {
            var request = await message.GetObjectDataAsync<AcquireLockRequest, IMessage>(token).ConfigureAwait(false);
            return new AcquireLockResponse 
            { 
                Content = await engine.RegisterAsync(request.LockName, request.LockInfo, token).ConfigureAwait(false)
            };
        }

        private async Task<IMessage> ReleaseLockAsync(IMessage message, CancellationToken token)
        {
            var request = await message.GetObjectDataAsync<ReleaseLockRequest, IMessage>(token).ConfigureAwait(false);
            return new ReleaseLockResponse
            {
                Content = await engine.UnregisterAsync(request.LockName, request.Owner, request.Version, token).ConfigureAwait(false)
            };
        }

        private async Task ForceUnlockAsync(IMessage message, CancellationToken token)
        {
            var request = await message.GetObjectDataAsync<ForcedUnlockRequest, IMessage>(token).ConfigureAwait(false);
            await engine.UnregisterAsync(request.LockName, token).ConfigureAwait(false);
        }

        Task IMessageHandler.ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token) => signal.Name switch
        {
            ForcedUnlockRequest.Name => ForceUnlockAsync(signal, token),
            _ => Task.FromException<IMessage>(new NotSupportedException())
        };

        Task<IMessage> IMessageHandler.ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token) => message.Name switch
        {
            AcquireLockRequest.Name => AcquireLockAsync(message, token),
            ReleaseLockRequest.Name => ReleaseLockAsync(message, token),
            _ => Task.FromException<IMessage>(new NotSupportedException())
        }; 

        bool IMessageHandler.IsSupported(string messageName, bool oneWay)
            => oneWay ? messageName.IsOneOf(ForcedUnlockRequest.Name) : messageName.IsOneOf(AcquireLockRequest.Name, ReleaseLockRequest.Name);

        private Task<bool> Release(string lockName, Guid version, CancellationToken token)
            => messageBus.SendMessageToLeaderAsync(new ReleaseLockRequest { Owner = owner, Version = version, LockName = lockName }, ReleaseLockResponse.Reader, token);
        
        private void Release(string lockName, in Guid version, TimeSpan timeout)
        {
            var released = false;
            if(engine.IsRegistered(lockName, in owner, version))
            {
                var task = Release(lockName, version, CancellationToken.None);
                try
                {
                    released = task.Wait(timeout) ? task.Result : throw new TimeoutException();
                }
                catch(AggregateException e)
                {
                    throw e.InnerException;
                }
                finally
                {
                    if(task.IsCompleted)
                        task.Dispose();
                }
            }
            if(!released)
                throw new SynchronizationLockException(ExceptionMessages.LockConflict);
        }

        internal async void ReleaseAsync(string lockName, Guid version, TimeSpan timeout)
        {
            if(engine.IsRegistered(lockName, in owner, in version))
                using(var timeoutSource = new CancellationTokenSource(timeout))
                    if(await Release(lockName, version, timeoutSource.Token).ConfigureAwait(false))
                        return;
            throw new SynchronizationLockException(ExceptionMessages.LockConflict);
        }

        private Action CreateReleaseAction(string lockName, Guid version, DistributedLockOptions options)
        {
            var timeout = options.LeaseTime;
            return options.ReleaseSynchronously ?
                new Action(() => Release(lockName, version, timeout)) :
                new Action(() => ReleaseAsync(lockName, version, timeout));
        }

        private async Task<Action?> TryAcquireLockAsync(string lockName, Timeout timeout, CancellationToken token)
        {
            var options = lockConfig?.Invoke(lockName) ?? DefaultOptions;
            var lockVersion = Guid.NewGuid();
            TimeSpan remainingTime;
            //send Acquire message to leader node
            //if leader doesn't confirm that the lock is acquired then wait for Release log entry
            //in local audit trail and then try again
            var request = new AcquireLockRequest 
            { 
                LockName = lockName,
                LockInfo = new DistributedLock
                {
                    Owner = owner,
                    Version = lockVersion,
                    LeaseTime = options.LeaseTime
                }
            };
            while(true)
            {
                request.LockInfo.CreationTime = DateTimeOffset.Now;
                if(await messageBus.SendMessageToLeaderAsync(request, AcquireLockResponse.Reader, token).ConfigureAwait(false))
                    break;
                if(timeout.RemainingTime.TryGetValue(out remainingTime) && await engine.WaitForLockEventAsync(false, remainingTime, token))
                    continue;
                goto acquisition_failed;
            }
            //acquisition confirmed by leader node so need to wait until the acquire command will be committed
            do
            {
                if(engine.IsRegistered(lockName, in owner, in lockVersion))
                    return CreateReleaseAction(lockName, lockVersion, options);
            }
            while(timeout.RemainingTime.TryGetValue(out remainingTime) && await engine.WaitForLockEventAsync(true, remainingTime, token));
        acquisition_failed:
            return null;
        }

        /// <summary>
        /// Gets the distributed lock.
        /// </summary>
        /// <param name="lockName">The name of distributed lock.</param>
        /// <returns>The distributed lock.</returns>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string; or contains invalid characters</exception>
        public AsyncLock this[string lockName]
        {
            get
            {
                engine.ValidateName(lockName);
                return new AsyncLock((timeout, token) => TryAcquireLockAsync(lockName, new Timeout(timeout), token));
            }
        }
        
        /// <summary>
        /// Sets distributed lock configuration provider.
        /// </summary>
        public DistributedLockConfigurationProvider Configuration
        {
            set => lockConfig = value;
        }

        public async void ForceUnlock(string lockName)
        {
            engine.ValidateName(lockName);
            await messageBus.SendSignalToLeaderAsync(new ForcedUnlockRequest { LockName = lockName }).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to create distributed lock provider.
        /// </summary>
        /// <typeparam name="TCluster">The type implementing cluster infrastructure.</typeparam>
        /// <param name="cluster">The cluster instance.</param>
        /// <returns>The lock provider; or <see langword="null"/> if cluster infrastructure is not compatible with distributed lock.</returns>
        public static DistributedLockProvider? TryCreate<TCluster>(TCluster cluster)
            where TCluster : class, IMessageBus, IReplicationCluster
            => cluster.GetService(typeof(IDistributedLockEngine)) is IDistributedLockEngine lockEngine ? new DistributedLockProvider(lockEngine, cluster) : null;
    }
}