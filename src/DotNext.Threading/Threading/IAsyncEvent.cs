namespace DotNext.Threading;

/// <summary>
/// Represents asynchronous event.
/// </summary>
public interface IAsyncEvent : IDisposable
{
    /// <summary>
    /// Determines whether this event in signaled state.
    /// </summary>
    bool IsSet { get; }

    /// <summary>
    /// Changes state of this even to non-signaled.
    /// </summary>
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    bool Reset();

    /// <summary>
    /// Raises asynchronous event if it meets internal conditions.
    /// </summary>
    /// <returns><see langword="true"/>, if state of this object changed from non-signaled to signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    bool Signal();

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <param name="timeout">The number of time to wait before this event is set.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default);

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask WaitAsync(CancellationToken token = default);
}