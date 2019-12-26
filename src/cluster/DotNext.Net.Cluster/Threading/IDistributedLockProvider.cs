using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents distributed exclusive lock manager.
    /// </summary>
    public interface IDistributedLockProvider 
    {
        /// <summary>
        /// Represents lock options.
        /// </summary>
        public class LockOptions
        {
            private TimeSpan? leaseTime;

            /// <summary>
            /// Gets or sets maximum lease time for the acquired lock.
            /// </summary>
            /// <remarks>
            /// The lock holder must acquire its ownership periodically
            /// in this time window.
            /// </remarks>
            public TimeSpan LeaseTime
            {
                get => leaseTime ?? TimeSpan.FromMinutes(1);
                set => leaseTime = value;
            }
        }

        /// <summary>
        /// Represents configuration provider for the distributed lock.
        /// </summary>
        /// <param name="lockName">The name of distributed lock.</param>
        /// <returns>The options of distributed lock; or <see langword="null"/> to use default options.</returns>
        public delegate LockOptions? ConfigurationProvider(string lockName);

        /// <summary>
        /// Gets the distributed lock.
        /// </summary>
        /// <param name="lockName">The name of distributed lock.</param>
        /// <returns>The distributed lock.</returns>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string.</exception>
        AsyncLock this[string lockName] { get; }

        /// <summary>
        /// Sets configuration provider for distributed locks.
        /// </summary>
        /// <value>The configuration provider.</value>
        ConfigurationProvider Configuration { set; }

        /// <summary>
        /// Releases the lock in unsafe manner.
        /// </summary>
        /// <param name="lockName">The name of distributed lock.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task represention unlock async operation.</returns>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task ForceUnlockAsync(string lockName, CancellationToken token = default);
    }
}