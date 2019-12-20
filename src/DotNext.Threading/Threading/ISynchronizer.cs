namespace DotNext.Threading
{
    /// <summary>
    /// Common interface for all synchronization primitives.
    /// </summary>
    public interface ISynchronizer
    {
        /// <summary>
        /// Indicates that one or more asynchronous callers are suspended.
        /// </summary>
        bool HasWaiters { get; }
    }
}