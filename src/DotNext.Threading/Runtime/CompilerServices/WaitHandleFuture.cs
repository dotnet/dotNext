using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents <see cref="WaitHandle"/> turned into awaitable future.
    /// </summary>
    public sealed class WaitHandleFuture : Future<Task<bool>>
    {
        internal static readonly WaitHandleFuture Successful = new WaitHandleFuture(true);
        internal static readonly WaitHandleFuture TimedOut = new WaitHandleFuture(false);

        private const int TimedOutState = 1;
        private const int SuccessfulState = 2;

        /// <summary>
        /// Represents object that is used to monitor the completion of an asynchronous operation
        /// </summary>
        public readonly struct Awaiter : INotifyCompletion
        {
            private readonly WaitHandleFuture handle;

            internal Awaiter(WaitHandleFuture handle) => this.handle = handle;

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
                    handle.OnCompleted(continuation);
            }
        }

        private readonly RegisteredWaitHandle handle;
        private int state;

        //constructor should be synchronized because OnTimeout can be called before than handle field will be set
        [MethodImpl(MethodImplOptions.Synchronized)]     
        internal WaitHandleFuture(WaitHandle wh, TimeSpan timeout)
            => handle = ThreadPool.RegisterWaitForSingleObject(wh, OnTimeout, null, timeout, true);

        private WaitHandleFuture(bool successful) => state = successful ? SuccessfulState : TimedOutState;

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

        /// <summary>
        /// Converts wait handle into <see cref="Task{TResult}"/>.
        /// </summary>
        /// <returns>The task representing wait handle.</returns>
        public override async Task<bool> AsTask() => await this;
    }
}