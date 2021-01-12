using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    using Threading.Tasks;
    using False = Generic.BooleanConst.False;
    using True = Generic.BooleanConst.True;

    /// <summary>
    /// Represents <see cref="WaitHandle"/> turned into awaitable future.
    /// </summary>
    public sealed class WaitHandleFuture : Future<Task<bool>>, Future.IAwaiter<bool>
    {
        internal static readonly WaitHandleFuture Successful = new WaitHandleFuture(true);
        internal static readonly WaitHandleFuture TimedOut = new WaitHandleFuture(false);

        private const int TimedOutState = 1;
        private const int SuccessfulState = 2;

        private readonly RegisteredWaitHandle handle;
        private int state;

        //constructor should be synchronized because OnTimeout can be called before than handle field will be set
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal WaitHandleFuture(WaitHandle wh, TimeSpan timeout)
            => handle = ThreadPool.RegisterWaitForSingleObject(wh, Complete, null, timeout, true);

        private WaitHandleFuture(bool successful) => state = successful ? SuccessfulState : TimedOutState;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Complete(object state, bool timedOut)
        {
            handle.Unregister(null);
            this.state = timedOut ? TimedOutState : SuccessfulState;
            Complete();
        }

        /// <summary>
        /// Indicates that the underlying wait handle is in signaled state.
        /// </summary>
        public override bool IsCompleted => state > 0;

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public IAwaiter<bool> GetAwaiter() => this;

        bool IAwaiter<bool>.GetResult()
        {
            switch (state)
            {
                case SuccessfulState:
                    return true;
                case TimedOutState:
                    return false;
                default:
                    throw new IncompletedFutureException();
            }
        }

        private async Task<bool> ExecuteAsync() => await this;

        /// <summary>
        /// Converts wait handle into <see cref="Task{TResult}"/>.
        /// </summary>
        /// <returns>The task representing wait handle.</returns>
        public override Task<bool> AsTask()
        {
            switch (state)
            {
                case SuccessfulState:
                    return CompletedTask<bool, True>.Task;
                case TimedOutState:
                    return CompletedTask<bool, False>.Task;
                default:
                    return ExecuteAsync();
            }
        }
    }
}