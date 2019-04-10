using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    /// <summary>
    /// Represents asynchronous flow synchronization event.
    /// </summary>
    /// <remarks>
    /// This is asynchronous version of <see cref="AutoResetEvent"/> and <see cref="ManualResetEvent"/>.
    /// </remarks>
    public class AsyncResetEvent: AsyncLockBase
    {
        /// <summary>
        /// Indicates that auto reset event is in signaled state.
        /// </summary>
        private sealed class AutoResetSignaledState: LockNode
        {
            internal AutoResetSignaledState() => SetResult(true);
        }

        private readonly bool autoReset;

        /// <summary>
        /// Initializes a new asynchronous reset event in nonsignaled state.
        /// </summary>
        /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to nonsignaled.</param>
        /// <param name="mode">Determines whether the event resets automatically or manually.</param>
        public AsyncResetEvent(bool initialState, EventResetMode mode)
        {
            switch(mode)
            {
                case EventResetMode.AutoReset: 
                    head = tail = initialState ? new AutoResetSignaledState() : null;
                    autoReset = true; 
                    break;
                case EventResetMode.ManualReset:
                    head = tail = initialState ? null : new LockNode();
                    autoReset = false; 
                    break;
                default: 
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>
        /// Gets whether this event is set.
        /// </summary>
        public bool IsSet
        {
            get
            {
                var head = Volatile.Read(ref this.head);
                return autoReset ? head is AutoResetSignaledState : head is null;
            }
        }

        /// <summary>
        /// Sets the state of this event to nonsignaled, causing consumers to wait asynchronously.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset()
        {
            if(autoReset)
                if(head is null) 
                    return false;
                else
                {
                    for(var current = head; !(current is null); current = current.CleanupAndGotoNext())
                        current.TrySetCanceled();
                    head = tail = null;
                    return true;
                }
            else if(head is null)   //is set for manual reset event
            {
                head = tail = new LockNode();
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Set()
        {
            if(head is null)
                if(autoReset)
                {
                    head = tail = new AutoResetSignaledState();
                    return true;
                }
                else
                    return false;
            head.Complete();    //fix deadlock issue with manual reset event
            return RemoveNode(head);
        }

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <param name="timeout">The interval to wait for the signaled state.</param>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> Wait(TimeSpan timeout, CancellationToken token)
        {
            /*
             * for ManualReset we perform a little optimization: share the same lock node between all awaiters
             */
            ThrowIfDisposed();
            if(timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            else if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            else if(autoReset ? head is AutoResetSignaledState : head is null)
            {
                head = null;
                return CompletedTask<bool, BooleanConst.True>.Task;
            }
            else if (timeout == TimeSpan.Zero)   //if timeout is zero fail fast
                return CompletedTask<bool, BooleanConst.False>.Task;
            else if(autoReset)  //enqueue awaiter
                tail = tail is null ? head = new LockNode() : new LockNode(tail);
            return timeout < TimeSpan.MaxValue || token.CanBeCanceled ? Wait(tail, timeout, token) : tail.Task;
        }

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <param name="timeout">The interval to wait for the signaled state.</param>
        /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
        public Task<bool> Wait(TimeSpan timeout) => Wait(timeout, CancellationToken.None);

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <remarks>
        /// This method can potentially blocks execution of async flow infinitely.
        /// </remarks>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns>A promise of signaled state.</returns>
        public Task Wait(CancellationToken token) => Wait(TimeSpan.MaxValue, token);

        /// <summary>
        /// Turns caller into idle state until the current event is set. 
        /// </summary>
        /// <remarks>
        /// This method can potentially blocks execution of async flow infinitely.
        /// </remarks>
        /// <returns>A promise of signaled state.</returns>
        public Task Wait() => Wait(CancellationToken.None);
    }
}