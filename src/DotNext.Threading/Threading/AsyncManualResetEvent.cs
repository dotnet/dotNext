using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous version of <see cref="ManualResetEvent"/>.
    /// </summary>
    public class AsyncManualResetEvent : Synchronizer, IAsyncResetEvent
    {
        /// <summary>
        /// Initializes a new asynchronous reset event in the specified state.
        /// </summary>
        /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
        public AsyncManualResetEvent(bool initialState)
        {
            node = initialState ? null : new ISynchronizer.WaitNode();
        }

        /// <inheritdoc/>
        EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.ManualReset;

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public bool Set() => Set(false);

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed;
        /// and, optionally, reverts the state of the event to initial state.
        /// </summary>
        /// <param name="autoReset"><see langword="true"/> to reset this object to non-signaled state automatically; <see langword="false"/> to leave this object in signaled state.</param>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Set(bool autoReset)
        {
            ThrowIfDisposed();
            var result = node?.TrySetResult(true) ?? false;
            node = autoReset ? new ISynchronizer.WaitNode() : null;
            return result;
        }

        /// <summary>
        /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset()
        {
            ThrowIfDisposed();
            if (node is null)
            {
                node = new ISynchronizer.WaitNode();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Signal() => Set();
    }
}