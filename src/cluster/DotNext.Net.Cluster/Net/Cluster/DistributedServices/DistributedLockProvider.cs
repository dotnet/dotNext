using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using Messaging;
    using Threading;
    using static IO.DataTransferObject;
    using IAuditTrail = IO.Log.IAuditTrail;
    
    /// <summary>
    /// Represents default implementation of distributed lock provider.
    /// </summary>
    /// <remarks>
    /// This type is not indendent to be used directly in your code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public sealed class DistributedLockProvider : IDistributedLockProvider, IInputChannel
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
        private readonly IOutputChannel leaderChannel;
        private readonly ClusterMemberId owner;
        private readonly IAuditTrail auditTrail;
        private readonly ILogger logger;

        internal DistributedLockProvider(IDistributedLockEngine engine, IOutputChannel leaderChannel, ClusterMemberId owner, IAuditTrail auditTrail, ILogger logger)
        {
            this.owner = owner;
            this.engine = engine;
            this.leaderChannel = leaderChannel;
            this.auditTrail = auditTrail;
            this.logger = logger;
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

        Task IInputChannel.ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token) => signal.Name switch
        {
            ForcedUnlockRequest.Name => ForceUnlockAsync(signal, token),
            _ => Task.FromException<IMessage>(new NotSupportedException())
        };

        Task<IMessage> IInputChannel.ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token) => message.Name switch
        {
            AcquireLockRequest.Name => AcquireLockAsync(message, token),
            ReleaseLockRequest.Name => ReleaseLockAsync(message, token),
            _ => Task.FromException<IMessage>(new NotSupportedException())
        }; 

        bool IInputChannel.IsSupported(string messageName, bool oneWay)
            => oneWay ? messageName.IsOneOf(ForcedUnlockRequest.Name) : messageName.IsOneOf(AcquireLockRequest.Name, ReleaseLockRequest.Name);

        private Task<bool> ReleaseAsync(string lockName, Guid version, CancellationToken token)
            => leaderChannel.SendMessageAsync(new ReleaseLockRequest { Owner = owner, Version = version, LockName = lockName }, ReleaseLockResponse.Reader, token);

        private async Task ReleaseAsync(string lockName, Guid version, TimeSpan timeout)
        {
            if(engine.IsRegistered(lockName, in owner, in version))
            {
                logger.ReleasingLock(lockName);
                using(var timeoutSource = new CancellationTokenSource(timeout))
                    if(await ReleaseAsync(lockName, version, timeoutSource.Token).ConfigureAwait(false))
                    {
                        logger.ReleaseLockConfirm(lockName);
                        return;
                    }
            }
            logger.FailedToUnlock(lockName);
            throw new SynchronizationLockException(ExceptionMessages.LockConflict);
        }

        private Func<Task> CreateReleaseAction(string lockName, Guid version, DistributedLockOptions options)
        {
            var timeout = options.LeaseTime;    //avoid capturing of entire options
            return () => ReleaseAsync(lockName, version, timeout);
        }

        private async Task<Func<Task>?> TryAcquireLockAsync(string lockName, Timeout timeout, CancellationToken token)
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
            long commitIndex;   //used for tracking commits
            while (true)
            {
                request.LockInfo.CreationTime = DateTimeOffset.Now;
                commitIndex = auditTrail.GetLastIndex(true);
                logger.AttemptsToAcquire(lockName);
                if (await leaderChannel.SendMessageAsync(request, AcquireLockResponse.Reader, token).ConfigureAwait(false))
                    break;
                if (timeout.RemainingTime.TryGetValue(out remainingTime) && await auditTrail.WaitForCommitAsync(commitIndex + 1L, remainingTime, token).ConfigureAwait(false))
                {
                    logger.PendingLockConfirmation(lockName);
                    continue;
                }
                goto acquisition_failed;
            }
            logger.AcquireLockConfirm(lockName);
            //acquisition confirmed by leader node so need to wait until the acquire command will be committed
            do
            {
                if (engine.IsRegistered(lockName, in owner, in lockVersion))
                    return CreateReleaseAction(lockName, lockVersion, options);
                else
                    commitIndex = auditTrail.GetLastIndex(true);
                logger.PendingLockCommit(lockName);
            }
            while (timeout.RemainingTime.TryGetValue(out remainingTime) && await auditTrail.WaitForCommitAsync(commitIndex + 1L, remainingTime, token).ConfigureAwait(false));
            acquisition_failed:
            logger.AcquireLockTimeout(lockName);
            return null;
        }

        /// <summary>
        /// Gets the distributed lock.
        /// </summary>
        /// <param name="lockName">The name of distributed lock.</param>
        /// <returns>The distributed lock.</returns>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string.</exception>
        public AsyncLock this[string lockName]
        {
            get
            {
                if (lockName.Length == 0)
                    throw new ArgumentException(ExceptionMessages.LockNameIsEmpty, nameof(lockName));
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
        
        /// <summary>
        /// Releases the lock even if it was not acquired
        /// by the current cluster member.
        /// </summary>
        /// <param name="lockName">The name of the lock to release.</param>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string.</exception>
        public async Task ForceUnlockAsync(string lockName)
        {
            if (lockName.Length == 0)
                    throw new ArgumentException(ExceptionMessages.LockNameIsEmpty, nameof(lockName));
            await leaderChannel.SendSignalAsync(new ForcedUnlockRequest { LockName = lockName }).ConfigureAwait(false);
        }
    }
}