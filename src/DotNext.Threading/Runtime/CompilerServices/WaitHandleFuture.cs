using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Runtime.CompilerServices
{
    using Threading.Tasks;

    /// <summary>
    /// Represents <see cref="WaitHandle"/> turned into awaitable future.
    /// </summary>
    internal sealed class WaitHandleFuture : Future<bool>, Future<bool>.IAwaiter
    {
        // cached to avoid allocations
        private static readonly WaitOrTimerCallback CompletionCallback = Complete!;

        private readonly RegisteredWaitHandle? handle;

        // constructor should be synchronized because OnTimeout can be called before than handle field will be set
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal WaitHandleFuture(WaitHandle wh, TimeSpan timeout)
            => handle = ThreadPool.RegisterWaitForSingleObject(wh, CompletionCallback, this, timeout, true);

        private static void Complete(object state, bool timedOut)
            => Unsafe.As<WaitHandleFuture>(state).Complete(timedOut);

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Complete(bool timedOut)
        {
            handle?.Unregister(null);
            base.Complete(timedOut);
        }

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public IAwaiter GetAwaiter() => this;

        /// <inheritdoc/>
        bool IAwaiter.GetResult() => GetResult();
    }
}