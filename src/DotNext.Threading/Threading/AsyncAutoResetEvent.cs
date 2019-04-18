using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous version of <see cref="AutoResetEvent"/>.
    /// </summary>
    public class AsyncAutoResetEvent: QueuedSynchronizer, IAsyncResetEvent
    {
        private readonly struct LockManager: ILockManager<bool, WaitNode>
        {
            bool ILockManager<bool, WaitNode>.CheckState(ref bool signaled)
            {
                if(signaled)
                {
                    signaled = false;
                    return true;
                }
                else
                    return false;
            }

            WaitNode ILockManager<bool, WaitNode>.CreateNode(WaitNode tail) => tail is null ? new WaitNode() : new WaitNode(tail);
        }
        private bool signaled;

        /// <summary>
        /// Initializes a new asynchronous reset event in the specified state.
        /// </summary>
        /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to nonsignaled.</param>
        public AsyncAutoResetEvent(bool initialState)
        {
            signaled = initialState;
        }

        /// <summary>
        /// Gets whether this event is set.
        /// </summary>
        public bool IsSet => signaled;

        /// <summary>
        /// Sets the state of this event to nonsignaled, causing consumers to wait asynchronously.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset()
        {
            ThrowIfDisposed();
            if(signaled)
            {
                signaled = false;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Set()
        {
            ThrowIfDisposed();
            if(signaled)
                return false;
            else if(head is null)
                return signaled = true;
            else
            {
                head.Complete();
                RemoveNode(head);
                signaled = false;
                return true;
            }
        }

        bool IAsyncEvent.Signal() => Set();

        EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.AutoReset;

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <param name="timeout">The interval to wait for the signaled state.</param>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> Wait(TimeSpan timeout, CancellationToken token) => Wait<bool, LockManager>(ref signaled, timeout, token);
    }
}