using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading.Runtime.CompilerServices
{
    /// <summary>
    /// Represents <see cref="WaitHandle"/> converted into awaitable task.
    /// </summary>
    public sealed class AwaitableWaitHandle
    {
        private static readonly WaitCallback ContinuationExecutor = RunContinuation;
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
            /// 
            /// </summary>
            /// <returns></returns>
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
                if(handle is null || handle.IsCompleted)
                    continuation();
                else
                    handle.AddContinuation(continuation);
            }
        }

        private readonly RegisteredWaitHandle handle;
        private volatile int state;
        private volatile Action continuation;

        internal AwaitableWaitHandle(WaitHandle handle, TimeSpan timeout)
            => this.handle = ThreadPool.RegisterWaitForSingleObject(handle, OnTimeout, null, timeout, true);

        private AwaitableWaitHandle(bool successful) => state = successful ? SuccessfulState : TimedOutState;

        private static void RunContinuation(object continuation)
        {
            if(continuation is Action action)
                action();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AddContinuation(Action continuation)
        {
            if(IsCompleted)
                ThreadPool.QueueUserWorkItem(ContinuationExecutor);   //leave synchronized method immediately
            else
                this.continuation += continuation;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnTimeout(object state, bool timedOut)
        {
            handle.Unregister(null);
            this.state = timedOut ? TimedOutState : SuccessfulState;
            ThreadPool.QueueUserWorkItem(ContinuationExecutor, continuation);   //leave synchronized method immediately
            continuation = null;
        }

        /// <summary>
        /// Indicates that the underlying wait handle is in signaled state.
        /// </summary>
        public bool IsCompleted => state > 0;

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public Awaiter GetAwaiter() => new Awaiter(this);
    }
}