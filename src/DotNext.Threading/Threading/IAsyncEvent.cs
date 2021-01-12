using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous event.
    /// </summary>
    public interface IAsyncEvent : ISynchronizer, IDisposable
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
        Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token);
    }

    /// <summary>
    /// Represents various extension methods for types implementing <see cref="IAsyncEvent"/> interface.
    /// </summary>
    public static class AsyncEvent
    {
        /// <summary>
        /// Turns caller into idle state until the current event is set.
        /// </summary>
        /// <param name="event">An event to synchronize with.</param>
        /// <param name="timeout">The interval to wait for the signaled state.</param>
        /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
        public static Task<bool> WaitAsync(this IAsyncEvent @event, TimeSpan timeout) => @event.WaitAsync(timeout, CancellationToken.None);

        /// <summary>
        /// Turns caller into idle state until the current event is set.
        /// </summary>
        /// <remarks>
        /// This method can potentially blocks execution of async flow infinitely.
        /// </remarks>
        /// <param name="event">An event to synchronize with.</param>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns>A promise of signaled state.</returns>
        public static Task WaitAsync(this IAsyncEvent @event, CancellationToken token) => @event.WaitAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Turns caller into idle state until the current event is set.
        /// </summary>
        /// <remarks>
        /// This method can potentially blocks execution of async flow infinitely.
        /// </remarks>
        /// <param name="event">An event to synchronize with.</param>
        /// <returns>A promise of signaled state.</returns>
        public static Task WaitAsync(this IAsyncEvent @event) => @event.WaitAsync(CancellationToken.None);
    }
}