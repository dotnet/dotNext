namespace DotNext.Threading
{
    /// <summary>
    /// Provides a set of methods to acquire different types of asynchronous lock.
    /// </summary>
    public static class AsyncLockManager
    {
        /// <summary>
        /// Destroy this lock and dispose underlying lock object if it is owned by the given lock.
        /// </summary>
        /// <remarks>
        /// If the given lock is an owner of the underlying lock object then this method will call <see cref="IDisposable.Dispose"/> on it;
        /// otherwise, the underlying lock object will not be destroyed.
        /// As a result, this lock is not usable after calling of this method.
        /// </remarks>
        public static void Destroy(this ref AsyncLock @lock)
        {
            @lock.DestroyUnderlyingLock();
            @lock = default;
        }
    }
}