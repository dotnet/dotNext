using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading;

using Runtime;
using Runtime.CompilerServices;

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

    private protected interface ITaskBuilder<out TOutput> : ITaskBuilder, ISupplier<TOutput>
        where TOutput : struct, IEquatable<TOutput>
    {
        static abstract bool ThrowOnTimeout { get; }

        static abstract TOutput FromException(Exception e);
    }
    
    private interface IValueTaskBuilder : ITaskBuilder<ValueTask>
    {
        static bool ITaskBuilder<ValueTask>.ThrowOnTimeout => true;

        static ValueTask ITaskBuilder<ValueTask>.FromException(Exception e)
            => ValueTask.FromException(e);
    }
    
    private interface IValueTaskBuilder<T> : ITaskBuilder<ValueTask<T>>
    {
        static bool ITaskBuilder<ValueTask<T>>.ThrowOnTimeout => false;

        static ValueTask<T> ITaskBuilder<ValueTask<T>>.FromException(Exception e)
            => ValueTask.FromException<T>(e);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected ref struct CancellationTokenOnly : IValueTaskBuilder
    {
        private readonly CancellationToken token;
        private ISupplier<TimeSpan, CancellationToken, ValueTask>? taskFactory;

        public CancellationTokenOnly(System.Threading.Lock syncRoot, CancellationToken token)
        {
            this.token = token;
            if (token.IsCancellationRequested)
            {
                taskFactory = CanceledTaskFactory.Instance;
            }
            else
            {
                syncRoot.Enter();
            }
        }

        [MemberNotNullWhen(true, nameof(taskFactory))]
        public readonly bool IsCompleted => taskFactory is not null;

        void ITaskBuilder.Complete(WaitNode factory) => taskFactory = factory;

        void ITaskBuilder.CompleteAsDisposed(string objectName) => taskFactory = new QueuedSynchronizerDisposedException(objectName);

        void ITaskBuilder.Complete() => taskFactory = CompletedTaskFactory.Instance;

        void ITaskBuilder.Complete<TFactory>() => taskFactory = ExceptionTaskFactory<TFactory>.Instance;

        readonly bool ITaskBuilder.TryCompleteAsTimedOut() => false;

        readonly ValueTask ISupplier<ValueTask>.Invoke()
        {
            Debug.Assert(IsCompleted);

            return taskFactory.Invoke(InfiniteTimeSpan, token);
        }

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(count, 0);

            SetDynamicInvokeResult<CancellationTokenOnly, ValueTask>(ref this, result);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private protected ref struct TimeoutAndCancellationToken :
        IValueTaskBuilder,
        IValueTaskBuilder<bool>
    {
        private readonly TimeSpan timeout;
        private readonly CancellationToken token;
        private IValueTaskFactory? taskFactory;

        public TimeoutAndCancellationToken(System.Threading.Lock syncRoot, TimeSpan timeout, CancellationToken token)
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
            else if (!syncRoot.TryEnter(timeout))
            {
                taskFactory = TimedOutTaskFactory.Instance;
            }
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

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(count, 0);
            Debug.Assert(IsCompleted);
            
            if (result.TargetType == typeof(ValueTask))
            {
                SetDynamicInvokeResult<TimeoutAndCancellationToken, ValueTask>(ref this, result);
            }
            else
            {
                SetDynamicInvokeResult<TimeoutAndCancellationToken, ValueTask<bool>>(ref this, result);
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private ref struct InterruptingTaskBuilder<T, TBuilder>
        : ITaskBuilder<T>
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        public required TBuilder Builder;
        public LinkedValueTaskCompletionSource<bool>? InterruptedCallers;

        void ITaskBuilder.Complete(WaitNode node) => Builder.Complete(node);

        void ITaskBuilder.CompleteAsDisposed(string objectName) => Builder.CompleteAsDisposed(objectName);

        void ITaskBuilder.Complete() => Builder.Complete();

        void ITaskBuilder.Complete<TFactory>() => Builder.Complete<TFactory>();

        bool ITaskBuilder.TryCompleteAsTimedOut() => Builder.TryCompleteAsTimedOut();

        public bool IsCompleted => Builder.IsCompleted;

        T ISupplier<T>.Invoke()
        {
            InterruptedCallers?.Unwind();
            return Builder.Invoke();
        }

        static bool ITaskBuilder<T>.ThrowOnTimeout => TBuilder.ThrowOnTimeout;

        static T ITaskBuilder<T>.FromException(Exception e) => TBuilder.FromException(e);

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => Builder.DynamicInvoke(in args, count, result);
    }

    private protected CancellationTokenOnly CreateTaskBuilder(CancellationToken token)
        => new(syncRoot, token);

    private protected TimeoutAndCancellationToken CreateTaskBuilder(TimeSpan timeout, CancellationToken token)
        => new(syncRoot, timeout, token);

    private protected T BuildTask<T, TBuilder>(ref TBuilder builder)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        if (syncRoot.IsHeldByCurrentThread)
        {
            syncRoot.Exit();
        }

        return builder.Invoke();
    }

    private protected T BuildTask<T, TBuilder>(Exception e)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        if (syncRoot.IsHeldByCurrentThread)
        {
            syncRoot.Exit();
        }

        return TBuilder.FromException(e);
    }

    private void DrainWaitQueue<T, TBuilder>(ref InterruptingTaskBuilder<T, TBuilder> builder, Exception e)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        if (!builder.IsCompleted)
            DrainWaitQueue<ExceptionVisitor>(new(e), out builder.InterruptedCallers);
    }

    private static void SetDynamicInvokeResult<TSupplier, TResult>(scoped ref TSupplier supplier, scoped Variant result)
        where TSupplier : struct, ISupplier<TResult>, allows ref struct
    {
        result.Mutable<TResult>() = supplier.Invoke();
    }
}