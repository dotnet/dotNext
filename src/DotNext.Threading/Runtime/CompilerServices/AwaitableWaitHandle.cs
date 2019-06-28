using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents <see cref="WaitHandle"/> converted into awaitable task.
    /// </summary>
    public sealed class AwaitableWaitHandle : Awaitable
    {
        internal static readonly AwaitableWaitHandle Successful = new AwaitableWaitHandle(true);
        internal static readonly AwaitableWaitHandle TimedOut = new AwaitableWaitHandle(false);

        private const int TimedOutState = 1;
        private const int SuccessfulState = 2;

        /// <summary>
        /// Represents object that is used to monitor the completion of an asynchronous operation
        /// </summary>
        public readonly struct Awaiter : INotifyCompletion
        {
            private readonly AwaitableWaitHandle handle;

            internal Awaiter(AwaitableWaitHandle handle) => this.handle = handle;

            /// <summary>
            /// Indicates that the underlying wait handle is in signaled state.
            /// </summary>
            public bool IsCompleted => handle is null || handle.IsCompleted;

            /// <summary>
            /// Gets result of waiting operation.
            /// </summary>
            /// <returns><see langword="true"/> if wait handle signaled before awaiter timed out; <see langword="false"/> if awaiter is timed out.</returns>
            public bool GetResult()
            {
                switch(handle?.state)
                {
                    case null:
                    case SuccessfulState:
                        return true;
                    case TimedOutState:
                        return false;
                    default:
                        throw new InvalidOperationException();
                }
            }

            /// <summary>
            /// Sets the continuation to invoke.
            /// </summary>
            /// <param name="continuation">The action to invoke asynchronously.</param>
            public void OnCompleted(Action continuation)
            {
                if(IsCompleted)
                    continuation();
                else
                    handle.AddContinuation(continuation);
            }
        }

        private readonly RegisteredWaitHandle handle;
        private int state;

        //constructor should be synchronized because OnTimeout can be called earlier than handle field will be set
        [MethodImpl(MethodImplOptions.Synchronized)]     
        internal AwaitableWaitHandle(WaitHandle wh, TimeSpan timeout)
            => handle = ThreadPool.RegisterWaitForSingleObject(wh, OnTimeout, null, timeout, true);

        private AwaitableWaitHandle(bool successful) => state = successful ? SuccessfulState : TimedOutState;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnTimeout(object state, bool timedOut)
        {
            handle.Unregister(null);   
            this.state = timedOut ? TimedOutState : SuccessfulState;
            continuation?.Invoke();
            continuation = null;
        }

        /// <summary>
        /// Indicates that the underlying wait handle is in signaled state.
        /// </summary>
        public override bool IsCompleted => state > 0;

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public Awaiter GetAwaiter() => new Awaiter(this);
    }
}