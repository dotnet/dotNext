using System;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents distributed exclusive lock manager.
    /// </summary>
    public interface IDistributedLockProvider 
    {
        /// <summary>
        /// Gets the distributed lock.
        /// </summary>
        /// <param name="lockName">The name of distributed lock.</param>
        /// <returns>The distributed lock.</returns>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string; or contains invalid characters</exception>
        AsyncLock this[string lockName] { get; }

        /// <summary>
        /// Sets configuration provider for distributed locks.
        /// </summary>
        /// <value>The configuration provider.</value>
        DistributedLockConfigurationProvider Configuration { set; }

        /// <summary>
        /// Releases the lock even if it was not acquired
        /// by the current cluster member.
        /// </summary>
        /// <remarks>
        /// This method should be used for maintenance purposes only if the particular lock is acquired for a long period of time
        /// and its owner crashed. In normal situation, this method can cause acquisition of the same lock by multiple requesters in a splitted cluster.
        /// </remarks>
        /// <param name="lockName">The name of the lock to release.</param>
        /// <exception cref="ArgumentException"><paramref name="lockName"/> is empty string; or contains invalid characters.</exception>
        Task ForceUnlockAsync(string lockName);
    }
}