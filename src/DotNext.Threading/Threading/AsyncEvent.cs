using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
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
        public static Task<bool> Wait(this IAsyncEvent @event, TimeSpan timeout) => @event.Wait(timeout, CancellationToken.None);

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <remarks>
        /// This method can potentially blocks execution of async flow infinitely.
        /// </remarks>
        /// <param name="event">An event to synchronize with.</param>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns>A promise of signaled state.</returns>
        public static Task Wait(this IAsyncEvent @event, CancellationToken token) => @event.Wait(InfiniteTimeSpan, token);

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <remarks>
        /// This method can potentially blocks execution of async flow infinitely.
        /// </remarks>
        /// <param name="event">An event to synchronize with.</param>
        /// <returns>A promise of signaled state.</returns>
        public static Task Wait(this IAsyncEvent @event) => @event.Wait(CancellationToken.None);
    }
}