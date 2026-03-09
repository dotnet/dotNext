using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private protected interface ITaskBuilder
    {
        void Complete(WaitNode node);

        void CompleteAsDisposed(string objectName);

        bool TryCompleteAsTimedOut();

        void Complete<TFactory>() where TFactory : IExceptionFactory, allows ref struct;

        void Complete();

        bool IsCompleted { get; }
    }

    private protected interface ITaskBuilder<out TOutput> : ITaskBuilder
        where TOutput : struct, IEquatable<TOutput>
    {
        TOutput Build();
        
        static abstract bool ThrowOnTimeout { get; }
    }
    
    private interface IValueTaskBuilder : ITaskBuilder<ValueTask>
    {
        static bool ITaskBuilder<ValueTask>.ThrowOnTimeout => true;
    }
    
    private interface IValueTaskBuilder<T> : ITaskBuilder<ValueTask<T>>
    {
        static bool ITaskBuilder<ValueTask<T>>.ThrowOnTimeout => false;
    }
    
    private protected interface IWaitQueueProvider : ITaskBuilder
    {
        WaitQueueScope CaptureWaitQueue();
    }

    [StructLayout(LayoutKind.Auto)]
    private protected ref struct CancellationTokenOnly : IValueTaskBuilder, IWaitQueueProvider
    {
        private readonly ref WaitQueue queue;
        private readonly CancellationToken token;
        private ISupplier<TimeSpan, CancellationToken, ValueTask>? taskFactory;

        public CancellationTokenOnly(ref WaitQueue queue, CancellationToken token)
        {
            this.token = token;
            if (token.IsCancellationRequested)
            {
                taskFactory = CanceledTaskFactory.Instance;
                this.queue = ref Unsafe.NullRef<WaitQueue>();
            }
            else
            {
                queue.SyncRoot.Enter();
                this.queue = ref queue;
            }
        }

        /// <summary>
        /// Captures the wait queue.
        /// </summary>
        /// <remarks>
        /// In contrast to <see cref="QueuedSynchronizer.CaptureWaitQueue"/> the caller should not call
        /// <see cref="WaitQueueScope.Dispose"/> to release the lock.
        /// </remarks>
        /// <returns>The captured wait queue.</returns>
        public readonly WaitQueueScope CaptureWaitQueue()
        {
            Debug.Assert(!Unsafe.IsNullRef(in queue));

            return new(ref queue);
        }

        [MemberNotNullWhen(true, nameof(taskFactory))]
        public readonly bool IsCompleted => taskFactory is not null;

        void ITaskBuilder.Complete(WaitNode factory) => taskFactory = factory;

        void ITaskBuilder.CompleteAsDisposed(string objectName) => taskFactory = new QueuedSynchronizerDisposedException(objectName);

        void ITaskBuilder.Complete() => taskFactory = CompletedTaskFactory.Instance;

        void ITaskBuilder.Complete<TFactory>() => taskFactory = ExceptionTaskFactory<TFactory>.Instance;

        readonly bool ITaskBuilder.TryCompleteAsTimedOut() => false;

        readonly ValueTask ITaskBuilder<ValueTask>.Build()
        {
            Debug.Assert(IsCompleted);

            if (!Unsafe.IsNullRef(in queue))
                queue.SyncRoot.Exit();

            return taskFactory.Invoke(InfiniteTimeSpan, token);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private protected ref struct TimeoutAndCancellationToken :
        IValueTaskBuilder,
        IValueTaskBuilder<bool>,
        IWaitQueueProvider
    {
        private readonly ref WaitQueue queue;
        private readonly TimeSpan timeout;
        private readonly CancellationToken token;
        private IValueTaskFactory? taskFactory;

        public TimeoutAndCancellationToken(ref WaitQueue queue, TimeSpan timeout, CancellationToken token)
        {
            this.timeout = timeout;
            this.token = token;
            if (!Timeout.IsValid(timeout))
            {
                taskFactory = InvalidTimeoutTaskFactory.Instance;
            }
            else if (token.IsCancellationRequested)
            {
                taskFactory = CanceledTaskFactory.Instance;
            }
            else if (queue.SyncRoot.TryEnter(timeout.Ticks is 0L ? InfiniteTimeSpan : timeout))
            {
                this.queue = ref queue;
                return;
            }
            else
            {
                taskFactory = TimedOutTaskFactory.Instance;
            }

            this.queue = ref Unsafe.NullRef<WaitQueue>();
        }

        /// <summary>
        /// Captures the wait queue.
        /// </summary>
        /// <remarks>
        /// In contrast to <see cref="QueuedSynchronizer.CaptureWaitQueue"/> the caller should not call
        /// <see cref="WaitQueueScope.Dispose"/> to release the lock.
        /// </remarks>
        /// <returns>The captured wait queue.</returns>
        public readonly WaitQueueScope CaptureWaitQueue()
        {
            Debug.Assert(!Unsafe.IsNullRef(in queue));

            return new(ref queue);
        }

        [MemberNotNullWhen(true, nameof(taskFactory))]
        public readonly bool IsCompleted => taskFactory is not null;

        void ITaskBuilder.Complete(WaitNode factory) => taskFactory = factory;

        void ITaskBuilder.CompleteAsDisposed(string objectName) => taskFactory = new QueuedSynchronizerDisposedException(objectName);

        void ITaskBuilder.Complete() => taskFactory = CompletedTaskFactory.Instance;

        void ITaskBuilder.Complete<TFactory>() => taskFactory = ExceptionTaskFactory<TFactory>.Instance;

        bool ITaskBuilder.TryCompleteAsTimedOut()
        {
            var timedOut = timeout is { Ticks: 0L };
            if (timedOut)
                taskFactory = TimedOutTaskFactory.Instance;

            return timedOut;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly T Build<T>(ISupplier<TimeSpan, CancellationToken, T> factory)
            where T : struct, IEquatable<T>
        {
            if (!Unsafe.IsNullRef(in queue))
                queue.SyncRoot.Exit();

            return factory.Invoke(timeout, token);
        }

        readonly ValueTask ITaskBuilder<ValueTask>.Build()
        {
            Debug.Assert(IsCompleted);

            return Build<ValueTask>(taskFactory);
        }
        
        readonly ValueTask<bool> ITaskBuilder<ValueTask<bool>>.Build()
        {
            Debug.Assert(IsCompleted);

            return Build<ValueTask<bool>>(taskFactory);
        }
    }

    private protected CancellationTokenOnly BeginAcquisition(CancellationToken token)
        => new(ref waitQueue, token);

    private protected TimeoutAndCancellationToken BeginAcquisition(TimeSpan timeout, CancellationToken token)
        => new(ref waitQueue, timeout, token);
}