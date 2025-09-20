using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private protected interface ITaskBuilder : IDisposable
    {
        void Complete(WaitNode node);

        void CompleteAsDisposed(string objectName);

        void CompleteAsTimedOut();

        void CompletedAsFull();

        void Complete();

        bool IsTimedOut { get; }

        bool IsCompleted { get; }
    }

    private protected interface ITaskBuilder<out TOutput> : ITaskBuilder, ISupplier<TOutput>
        where TOutput : struct, IEquatable<TOutput>
    {
        static abstract bool ThrowOnTimeout { get; }

        static abstract TOutput FromException(Exception e);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected struct CancellationTokenOnly : ITaskBuilder<ValueTask>
    {
        private readonly CancellationToken token;
        private object? syncRoot;
        private ISupplier<TimeSpan, CancellationToken, ValueTask>? taskFactory;

        public CancellationTokenOnly(object syncRoot, CancellationToken token)
        {
            this.token = token;
            if (token.IsCancellationRequested)
            {
                taskFactory = CanceledTaskFactory.Instance;
            }
            else
            {
                Monitor.Enter(this.syncRoot = syncRoot);
            }
        }

        [MemberNotNullWhen(true, nameof(taskFactory))]
        public readonly bool IsCompleted => taskFactory is not null;

        void ITaskBuilder.Complete(WaitNode factory) => taskFactory = factory;

        void ITaskBuilder.CompleteAsDisposed(string objectName) => taskFactory = new QueuedSynchronizerDisposedException(objectName);

        void ITaskBuilder.CompleteAsTimedOut() => taskFactory = TimedOutTaskFactory.Instance;

        void ITaskBuilder.Complete() => taskFactory = CompletedTaskFactory.Instance;

        void ITaskBuilder.CompletedAsFull() => taskFactory = ConcurrencyLimitReachedTaskFactory.Instance;

        readonly bool ITaskBuilder.IsTimedOut => false;

        void IDisposable.Dispose()
        {
            if (syncRoot is not null)
            {
                Monitor.Exit(syncRoot);
                syncRoot = null;
            }
        }

        readonly ValueTask ISupplier<ValueTask>.Invoke()
        {
            Debug.Assert(IsCompleted);

            return taskFactory.Invoke(InfiniteTimeSpan, token);
        }

        static bool ITaskBuilder<ValueTask>.ThrowOnTimeout => true;

        static ValueTask ITaskBuilder<ValueTask>.FromException(Exception e) => ValueTask.FromException(e);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected struct TimeoutAndCancellationToken :
        ITaskBuilder<ValueTask>,
        ITaskBuilder<ValueTask<bool>>
    {
        private readonly TimeSpan timeout;
        private readonly CancellationToken token;
        private object? syncRoot;
        private IValueTaskFactory? taskFactory;

        public TimeoutAndCancellationToken(object syncRoot, TimeSpan timeout, CancellationToken token)
        {
            this.timeout = timeout;
            this.token = token;
            if (timeout is { Ticks: < 0L and not Timeout.InfiniteTicks or > Timeout.MaxTimeoutParameterTicks })
            {
                taskFactory = InvalidTimeoutTaskFactory.Instance;
            }
            else if (token.IsCancellationRequested)
            {
                taskFactory = CanceledTaskFactory.Instance;
            }
            else if (Monitor.TryEnter(syncRoot, timeout))
            {
                this.syncRoot = syncRoot;
            }
            else
            {
                taskFactory = TimedOutTaskFactory.Instance;
            }
        }

        [MemberNotNullWhen(true, nameof(taskFactory))]
        public readonly bool IsCompleted => taskFactory is not null;

        void ITaskBuilder.Complete(WaitNode factory) => taskFactory = factory;

        void ITaskBuilder.CompleteAsDisposed(string objectName) => taskFactory = new QueuedSynchronizerDisposedException(objectName);

        void ITaskBuilder.CompleteAsTimedOut() => taskFactory = TimedOutTaskFactory.Instance;

        void ITaskBuilder.Complete() => taskFactory = CompletedTaskFactory.Instance;
        
        void ITaskBuilder.CompletedAsFull() => taskFactory = ConcurrencyLimitReachedTaskFactory.Instance;

        readonly bool ITaskBuilder.IsTimedOut => timeout is { Ticks: 0L };

        void IDisposable.Dispose()
        {
            if (syncRoot is not null)
            {
                Monitor.Exit(syncRoot);
                syncRoot = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly T AsTask<T>(ISupplier<TimeSpan, CancellationToken, T> factory)
            where T : struct, IEquatable<T>
            => factory.Invoke(timeout, token);

        readonly ValueTask ISupplier<ValueTask>.Invoke()
        {
            Debug.Assert(IsCompleted);

            return AsTask<ValueTask>(taskFactory);
        }
        
        readonly ValueTask<bool> ISupplier<ValueTask<bool>>.Invoke()
        {
            Debug.Assert(IsCompleted);

            return AsTask<ValueTask<bool>>(taskFactory);
        } 

        static bool ITaskBuilder<ValueTask>.ThrowOnTimeout => true;

        static bool ITaskBuilder<ValueTask<bool>>.ThrowOnTimeout => false;
        
        static ValueTask ITaskBuilder<ValueTask>.FromException(Exception e) => ValueTask.FromException(e);
        
        static ValueTask<bool> ITaskBuilder<ValueTask<bool>>.FromException(Exception e) => ValueTask.FromException<bool>(e);
    }

    [StructLayout(LayoutKind.Auto)]
    private struct InterruptingTaskBuilder<T, TBuilder>
        : ITaskBuilder<T>
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>
    {
        public required TBuilder Builder;
        public LinkedValueTaskCompletionSource<bool>? InterruptedCallers;

        void IDisposable.Dispose()
        {
            Builder.Dispose();
            InterruptedCallers?.Unwind();
        }

        void ITaskBuilder.Complete(WaitNode node) => Builder.Complete(node);

        void ITaskBuilder.CompleteAsDisposed(string objectName) => Builder.CompleteAsDisposed(objectName);

        void ITaskBuilder.CompleteAsTimedOut() => Builder.CompleteAsTimedOut();

        void ITaskBuilder.Complete() => Builder.Complete();

        void ITaskBuilder.CompletedAsFull() => Builder.CompletedAsFull();

        bool ITaskBuilder.IsTimedOut => Builder.IsTimedOut;

        public bool IsCompleted => Builder.IsCompleted;

        T ISupplier<T>.Invoke() => Builder.Invoke();

        static bool ITaskBuilder<T>.ThrowOnTimeout => TBuilder.ThrowOnTimeout;

        static T ITaskBuilder<T>.FromException(Exception e) => TBuilder.FromException(e);
    }

    private protected CancellationTokenOnly CreateTaskBuilder(CancellationToken token)
        => new(SyncRoot, token);

    private protected TimeoutAndCancellationToken CreateTaskBuilder(TimeSpan timeout, CancellationToken token)
        => new(SyncRoot, timeout, token);

    private void DrainWaitQueue<T, TBuilder>(ref InterruptingTaskBuilder<T, TBuilder> builder, Exception e)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>
        => builder.InterruptedCallers = builder.IsCompleted
            ? null
            : DrainWaitQueue(e);
}