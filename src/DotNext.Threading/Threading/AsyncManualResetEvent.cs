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
            node = initialState ? null : new WaitNode();
        }

        EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.ManualReset;

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Set()
        {
            ThrowIfDisposed();
            if (node is null)    //already in signaled state
                return false;
            else if (node.TrySetResult(true))
            {
                node = null;
                return true;
            }
            else
                return false;
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
                node = new WaitNode();
                return true;
            }
            else
                return false;
        }

        bool IAsyncEvent.Signal() => Set();
    }
}