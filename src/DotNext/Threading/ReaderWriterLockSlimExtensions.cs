using System.Threading;

namespace DotNext.Threading
{
    public static class ReaderWriterLockSlimExtensions
    {
        /// <summary>
        /// Returns writer lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>Writer lock.</returns>
        public static Lock WriteLock(this ReaderWriterLockSlim rwLock) => Lock.WriteLock(rwLock);

        /// <summary>
        /// Returns reader lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>Reade lock.</returns>
        public static Lock ReadLock(this ReaderWriterLockSlim rwLock) => Lock.ReadLock(rwLock, false);

        /// <summary>
        /// Returns upgradable reader lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>Upgradable read lock.</returns>
        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock) => Lock.ReadLock(rwLock, true);
    }
}
