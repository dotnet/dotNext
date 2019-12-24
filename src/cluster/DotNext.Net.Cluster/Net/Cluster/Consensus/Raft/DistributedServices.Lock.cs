using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;

    public partial class DistributedServices : ILockManager
    {
        /// <summary>
        /// Gets the prefix of log entry that represents
        /// distributed lock command.
        /// </summary>
        [CLSCompliant(false)]
        protected const uint LockCommandId = 0xedb88320;

        /// <summary>
        /// Represents the command associated with
        /// distributed lock.
        /// </summary>
        protected enum LockCommand : short
        {
            Nop = 0,
            Release,
            Acquire,
        }

        private sealed class LockProvider
        {
            private readonly DistributedServices manager;
            private readonly string name;

            internal LockProvider(DistributedServices manager, string name)
            {
                this.name = name;
                this.manager = manager;
            }

            private Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken token)
                => manager.TryAcquireLockAsync(name, timeout, token);

            public static implicit operator AsyncLock.Acquisition(LockProvider provider) => provider.TryAcquireAsync;
        }

        private ILockManager.ConfigurationProvider? lockConfig;

        private Task<bool> TryAcquireLockAsync(string lockName, TimeSpan timeout, CancellationToken token)
        {

        }

        public AsyncLock GetLock(string lockName)
        {

        }
        

        public ILockManager.ConfigurationProvider LockConfiguration
        {
            set => lockConfig = value;
        }

        AsyncLock ILockManager.this[string lockName] => GetLock(lockName);
        ILockManager.ConfigurationProvider ILockManager.Configuration
        {
            set => LockConfiguration = value;
        }

        public Task ForceUnlockAsync(string lockName, CancellationToken token = default)
        {

        }
    }
}