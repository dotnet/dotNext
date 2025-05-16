namespace DotNext.Threading;

/// <summary>
/// The exception thrown when pending asynchronous task is interrupted while
/// it is in waiting state.
/// </summary>
/// <seealso cref="ThreadInterruptedException"/>
/// <param name="message">The error message that explains the reason for the exception.</param>
/// <param name="innerException">The exception that is the cause of the current exception.</param>
public class PendingTaskInterruptedException(string? message = null, Exception? innerException = null) : Exception(message ?? ExceptionMessages.AsyncTaskInterrupted, innerException)
{
    /// <summary>
    /// Gets the reason for lock steal.
    /// </summary>
    public object? Reason
    {
        get;
        init;
    }
}