namespace DotNext.Threading;

/// <summary>
/// Indicates that the internal queue of the synchronization primitive is full.
/// </summary>
/// <seealso cref="QueuedSynchronizer"/>
public sealed class ConcurrencyLimitReachedException : Exception
{
    internal ConcurrencyLimitReachedException()
        : base(ExceptionMessages.ConcurrencyLimitReached)
    {
    }
}