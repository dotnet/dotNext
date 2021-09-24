namespace DotNext.Threading;

/// <summary>
/// Represents asynchronous flow synchronization event.
/// </summary>
public interface IAsyncResetEvent : IAsyncEvent
{
    /// <summary>
    /// Gets reset mode.
    /// </summary>
    EventResetMode ResetMode { get; }
}