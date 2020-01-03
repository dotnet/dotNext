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
    public sealed class DistributedLockProvider : IDistributedLockProvider, IDistributedServiceProvider
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
            return new ReleaseLockResponse()
            {
                Content = await engine.UnregisterAsync(request.LockName, request.Owner, request.Version, token).ConfigureAwait(false)
            };
        }

        private async Task ForceUnlockAsync(IMessage message, CancellationToken token)
        {
            var request = await message.GetObjectDataAsync<ForcedUnlockRequest, IMessage>(token).ConfigureAwait(false);
            await engine.UnregisterAsync(request.LockName, token).ConfigureAwait(false);
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
        public Task<IMessage> ProcessMessage(IMessage message, CancellationToken token) => message.Name switch
        {
            AcquireLockRequest.Name => AcquireLockAsync(message, token),
            ReleaseLockRequest.Name => ReleaseLockAsync(message, token),
            _ => Task.FromException<IMessage>(new NotSupportedException())
        };

        /// <summary>
        /// Handles one-way service message related to distributed lock management.
        /// </summary>
        /// <remarks>
        /// Ensure that <see cref="IsMessageSupported"/> returned true before
        /// calling this method.
        /// </remarks>
        /// <param name="signal">The message to process.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous message processing.</returns>
        public Task ProcessSignal(IMessage signal, CancellationToken token) => signal.Name switch
        {
            ForcedUnlockRequest.Name => ForceUnlockAsync(signal, token),
            _ => Task.FromException<IMessage>(new NotSupportedException())
        };

        /// <summary>
        /// Determines whether the specified message is a service message for distributed lock management.
        /// </summary>
        /// <param name="message">The message to check.</param>
        /// <param name="oneWay"><see langword="true"/> if <paramref name="message"/> is one-way message; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if the specified message can be handled by distributed lock provider; otherwise, <see langword="false"/>.</returns>
        public static bool IsMessageSupported(IMessage message, bool oneWay)
            => oneWay ? message.Name.IsOneOf(ForcedUnlockRequest.Name) : message.Name.IsOneOf(AcquireLockRequest.Name, ReleaseLockRequest.Name);

        private Action ReleaseAction(string lockName, Guid version, TimeSpan timeout)
            => new Action(() => Unlock(lockName, version, timeout));

        private async Task<Action?> TryAcquireLockAsync(string lockName, Timeout timeout, CancellationToken token)
        {
            var options = lockConfig?.Invoke(lockName) ?? DefaultOptions;
            //send Acquire message to leader node
            //if leader doesn't confirm that the lock is acquired then wait for Release log entry
            //in local audit trail and then try again
            var timeoutSource = new TimeoutTokenSource(timeout, token);
            var eventListener = engine.OnRelease(timeoutSource.Token);
            var lockVersion = Guid.NewGuid();
            try
            {
                for(var request = new AcquireLockRequest { LockName = lockName }; ; await eventListener.SuspendAsync().ConfigureAwait(false))
                {
                    request.LockInfo = new DistributedLockInfo
                    {
                        Owner = engine.NodeId,
                        Version = lockVersion,
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
            eventListener = engine.OnAcquire(timeoutSource.Token);
            try
            {
                while(!engine.IsRegistered(lockName, lockVersion))
                    await eventListener.SuspendAsync().ConfigureAwait(false);
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
            return timeout.RemainingTime.HasValue ?
                ReleaseAction(lockName, lockVersion, options.LeaseTime) :
                null;
        }

        private async Task UnlockAsync(string lockName, Guid version)
        {
            var request = new ReleaseLockRequest
            {
                Owner = engine.NodeId,
                Version = version,
                LockName = lockName
            };
            if(!(await messageBus.SendMessageToLeaderAsync(request, ReleaseLockResponse.Reader).ConfigureAwait(false)))
                throw new SynchronizationLockException(ExceptionMessages.LockConflict);
        }

        private void Unlock(string lockName, Guid version, TimeSpan timeout)
        {
            //fail fast - check the local state
            if(!engine.IsRegistered(lockName, version))
                throw new SynchronizationLockException(ExceptionMessages.LockConflict);
            //slow path - inform the leader node
            var task = UnlockAsync(lockName, version);
            try
            {
                if(!task.Wait(timeout))
                    throw new TimeoutException();
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

        /// <summary>
        /// Initializes distributed lock infrastructure.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        public Task InitializeAsync(CancellationToken token)
            => engine.RestoreAsync(token);

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
            => cluster.AuditTrail is IDistributedLockEngine lockEngine ? new DistributedLockProvider(lockEngine, cluster) : null;
    }
}