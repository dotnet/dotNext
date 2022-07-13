namespace DotNext.Threading;

/// <summary>
/// The exception thay is thrown when pending asynchronous task is interrupted while
/// it is in waiting state.
/// </summary>
/// <seealso cref="ThreadInterruptedException"/>
public sealed class PendingTaskInterruptedException : Exception
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public PendingTaskInterruptedException()
        : base(ExceptionMessages.AsyncTaskInterrupted)
    {
    }

    /// <summary>
    /// Gets the reason for lock steal.
    /// </summary>
    public object? Reason
    {
        get;
        init;
    }
}