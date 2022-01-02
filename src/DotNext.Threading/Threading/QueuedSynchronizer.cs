using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

using Generic;
using Tasks;
using Tasks.Pooling;
using Timestamp = Diagnostics.Timestamp;

/// <summary>
/// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
/// </summary>
public class QueuedSynchronizer : Disposable
{
    private sealed class CallerInformationStorage : ThreadLocal<object?>
    {
        internal CallerInformationStorage()
            : base(trackAllValues: false)
        {
        }

        internal CallerInformationStorage(Func<object> callerInfoProvider)
            : base(callerInfoProvider, trackAllValues: false)
        {
        }

        internal object? Capture()
        {
            var result = Value;
            if (result is null)
            {
                result = Activity.Current ?? Trace.CorrelationManager.LogicalOperationStack.Peek();
            }
            else
            {
                Value = null;
            }

            return result;
        }
    }

    private protected abstract class WaitNode : LinkedValueTaskCompletionSource<bool>
    {
        private Timestamp createdAt;
        private Action<double>? lockDurationCounter;
        private bool throwOnTimeout;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo
        {
            get;
            private set;
        }

        private protected override void ResetCore()
        {
            lockDurationCounter = null;
            CallerInfo = null;
            base.ResetCore();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(bool throwOnTimeout, Action<double>? lockDurationCounter, object? callerInfo)
        {
            this.throwOnTimeout = throwOnTimeout;
            this.lockDurationCounter = lockDurationCounter;
            CallerInfo = callerInfo;
            createdAt = new();
        }

        protected sealed override Result<bool> OnTimeout() => throwOnTimeout ? base.OnTimeout() : false;

        private void ReportLockDuration()
            => lockDurationCounter?.Invoke(createdAt.Elapsed.TotalMilliseconds);

        private protected static void AfterConsumed<T>(T node)
            where T : WaitNode, IPooledManualResetCompletionSource<Action<T>>
        {
            node.ReportLockDuration();
            node.As<IPooledManualResetCompletionSource<Action<T>>>().OnConsumed?.Invoke(node);
        }
    }

    private protected sealed class DefaultWaitNode : WaitNode, IPooledManualResetCompletionSource<Action<DefaultWaitNode>>
    {
        private Action<DefaultWaitNode>? consumedCallback;

        protected sealed override void AfterConsumed() => AfterConsumed(this);

        ref Action<DefaultWaitNode>? IPooledManualResetCompletionSource<Action<DefaultWaitNode>>.OnConsumed => ref consumedCallback;
    }

    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();
    }

    private protected interface ILockManager<in TNode> : ILockManager
        where TNode : WaitNode
    {
        void InitializeNode(TNode node);
    }

    // This type allows to create the task out of the lock to reduce lock contention
    [StructLayout(LayoutKind.Auto)]
    private protected readonly ref struct ValueTaskFactory
    {
        // null - successfully completed task
        // Task - completed task
        // ISupplier<TimeSpan, CancellationToken, ValueTask> - completion source
        private readonly object? result;

        private ValueTaskFactory(Task task)
        {
            Debug.Assert(task is { IsCompleted: true });

            result = task;
        }

        private ValueTaskFactory(ISupplier<TimeSpan, CancellationToken, ValueTask> source)
        {
            Debug.Assert(source is not null);

            result = source;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask Create(TimeSpan timeout, CancellationToken token)
        {
            Debug.Assert(result is null or Task or ISupplier<TimeSpan, CancellationToken, ValueTask>);

            return result switch
            {
                null => ValueTask.CompletedTask,
                Task t => new(t),
                object source => Unsafe.As<ISupplier<TimeSpan, CancellationToken, ValueTask>>(source).Invoke(timeout, token)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask Create(CancellationToken token) => Create(InfiniteTimeSpan, token);

        internal static ValueTaskFactory FromCanceled(CancellationToken token)
            => FromTask(Task.FromCanceled(token));

        internal static ValueTaskFactory FromException(Exception e)
            => FromTask(Task.FromException(e));

        internal static ValueTaskFactory FromTask(Task t)
            => new(t);

        internal static ValueTaskFactory Completed => default;

        internal static ValueTaskFactory FromSource(ISupplier<TimeSpan, CancellationToken, ValueTask> source)
            => new(source);
    }

    // This type allows to create the task out of the lock to reduce lock contention
    [StructLayout(LayoutKind.Auto)]
    private protected readonly ref struct ValueTaskFactory<T>
    {
        // Task<T> - completed task
        // ISupplier<TimeSpan, CancellationToken, ValueTask<T>> - completion source
        private readonly object? result;

        private ValueTaskFactory(Task<T> task)
        {
            Debug.Assert(task is { IsCompleted: true });

            result = task;
        }

        private ValueTaskFactory(ISupplier<TimeSpan, CancellationToken, ValueTask<T>> source)
        {
            Debug.Assert(source is not null);

            result = source;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<T> Create(TimeSpan timeout, CancellationToken token)
        {
            Debug.Assert(result is null or Task<T> or ISupplier<TimeSpan, CancellationToken, ValueTask<T>>);

            return result switch
            {
                null => new(),
                Task<T> t => new(t),
                object source => Unsafe.As<ISupplier<TimeSpan, CancellationToken, ValueTask<T>>>(source).Invoke(timeout, token)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<T> Create(CancellationToken token) => Create(InfiniteTimeSpan, token);

        internal static ValueTaskFactory<T> FromCanceled(CancellationToken token)
            => FromTask(Task.FromCanceled<T>(token));

        internal static ValueTaskFactory<T> FromException(Exception e)
            => FromTask(Task.FromException<T>(e));

        internal static ValueTaskFactory<T> FromSource(ISupplier<TimeSpan, CancellationToken, ValueTask<T>> source)
            => new(source);

        internal static ValueTaskFactory<T> FromResult<TResult>()
            where TResult : Constant<T>, new()
            => FromTask(CompletedTask<T, TResult>.Task);

        internal static ValueTaskFactory<T> FromTask(Task<T> task)
            => new(task);
    }

    private readonly Action<double>? contentionCounter, lockDurationCounter;
    private readonly TaskCompletionSource disposeTask;
    private CallerInformationStorage? callerInfo;
    private protected LinkedValueTaskCompletionSource<bool>? first;
    private LinkedValueTaskCompletionSource<bool>? last;

    private protected QueuedSynchronizer()
    {
        disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private protected bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node)
    {
        RemoveNode(node);
        return node.TrySetResult(Sentinel.Instance, value: true);
    }

    private protected bool IsDisposeRequested
    {
        get;
        private set;
    }

    /// <summary>
    /// Enables capturing information about suspended callers in DEBUG configuration.
    /// </summary>
    /// <remarks>
    /// If <paramref name="callerInfoProvider"/> is provided then no need to use <see cref="SetCallerInformation(object)"/>.
    /// </remarks>
    /// <param name="callerInfoProvider">The optional factory of the information about the caller.</param>
    [Conditional("DEBUG")]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void TrackSuspendedCallers(Func<object>? callerInfoProvider = null)
        => callerInfo = callerInfoProvider is null ? new() : new(callerInfoProvider);

    /// <summary>
    /// Sets caller information in DEBUG configuration.
    /// </summary>
    /// <remarks>
    /// It is recommended to inject caller information immediately before calling of <c>WaitAsync</c> method.
    /// </remarks>
    /// <param name="information">The object that identifies the caller.</param>
    [Conditional("DEBUG")]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void SetCallerInformation(object information)
    {
        ArgumentNullException.ThrowIfNull(information);

        if (callerInfo is not null)
            callerInfo.Value = information;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private IReadOnlyList<object?> GetSuspendedCallersCore()
    {
        if (first is null)
            return Array.Empty<Activity?>();

        var list = new List<object?>();
        for (LinkedValueTaskCompletionSource<bool>? current = first; current is not null; current = current.Next)
        {
            if (current is WaitNode node)
                list.Add(node.CallerInfo);
        }

        list.TrimExcess();
        return list;
    }

    /// <summary>
    /// Gets a list of suspended callers respecting their order in wait queue.
    /// </summary>
    /// <remarks>
    /// This method is introduced for debugging purposes only.
    /// </remarks>
    /// <returns>A list of suspended callers.</returns>
    /// <seealso cref="TrackSuspendedCallers"/>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IReadOnlyList<object?> GetSuspendedCallers()
        => callerInfo is null ? Array.Empty<object?>() : GetSuspendedCallersCore();

    /// <summary>
    /// Sets counter for lock contention.
    /// </summary>
    public IncrementingEventCounter LockContentionCounter
    {
        init => contentionCounter = (value ?? throw new ArgumentNullException(nameof(value))).Increment;
    }

    /// <summary>
    /// Sets counter of lock duration, in milliseconds.
    /// </summary>
    public EventCounter LockDurationCounter
    {
        init => lockDurationCounter = (value ?? throw new ArgumentNullException(nameof(value))).WriteMetric;
    }

    private protected bool RemoveNode(LinkedValueTaskCompletionSource<bool> node)
    {
        Debug.Assert(Monitor.IsEntered(this));

        bool isFirst;

        if (isFirst = ReferenceEquals(first, node))
            first = node.Next;

        if (ReferenceEquals(last, node))
            last = node.Previous;

        node.Detach();
        return isFirst;
    }

    private TNode EnqueueNode<TNode, TLockManager>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, bool throwOnTimeout, object? callerInfo)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        var node = pool.Get();
        manager.InitializeNode(node);
        node.Initialize(throwOnTimeout, lockDurationCounter, callerInfo);

        if (last is null)
        {
            first = last = node;
        }
        else
        {
            last.Append(node);
            last = node;
        }

        contentionCounter?.Invoke(1L);
        return node;
    }

    private protected bool TryAcquire<TLockManager>(ref TLockManager manager)
        where TLockManager : struct, ILockManager
    {
        Debug.Assert(Monitor.IsEntered(this));

        bool result;

        if (result = manager.IsLockAllowed)
        {
            for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
            {
                next = current.Next;

                if (!current.IsCompleted)
                {
                    result = false;
                    goto exit;
                }

                RemoveNode(current);
            }

            manager.AcquireLock();
        }

    exit:
        return result;
    }

    private protected ValueTaskFactory WaitWithTimeoutAsync<TNode, TLockManager>(ref TLockManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TimeSpan timeout, CancellationToken token)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        ValueTaskFactory result;
        var callerInfo = this.callerInfo?.Capture();

        if (IsDisposed || IsDisposeRequested)
        {
            result = ValueTaskFactory.FromTask(DisposedTask);
        }
        else if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
        {
            result = ValueTaskFactory.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
        }
        else if (token.IsCancellationRequested)
        {
            result = ValueTaskFactory.FromCanceled(token);
        }
        else if (TryAcquire(ref manager))
        {
            result = ValueTaskFactory.Completed;
        }
        else if (timeout == TimeSpan.Zero)
        {
            result = ValueTaskFactory.FromException(new TimeoutException());
        }
        else
        {
            result = ValueTaskFactory.FromSource(EnqueueNode(ref pool, ref manager, throwOnTimeout: true, callerInfo));
        }

        return result;
    }

    // optimized version without timeout support
    private protected ValueTaskFactory WaitNoTimeoutAsync<TNode, TLockManager>(ref TLockManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, CancellationToken token)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        ValueTaskFactory result;
        var callerInfo = this.callerInfo?.Capture();

        if (IsDisposed || IsDisposeRequested)
        {
            result = ValueTaskFactory.FromTask(DisposedTask);
        }
        else if (token.IsCancellationRequested)
        {
            result = ValueTaskFactory.FromCanceled(token);
        }
        else if (TryAcquire(ref manager))
        {
            result = ValueTaskFactory.Completed;
        }
        else
        {
            result = ValueTaskFactory.FromSource(EnqueueNode(ref pool, ref manager, throwOnTimeout: false, callerInfo));
        }

        return result;
    }

    private protected ValueTaskFactory<bool> WaitNoTimeoutAsync<TNode, TManager>(ref TManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TimeSpan timeout, CancellationToken token)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        ValueTaskFactory<bool> result;
        var callerInfo = this.callerInfo?.Capture();

        if (IsDisposed || IsDisposeRequested)
        {
            result = ValueTaskFactory<bool>.FromTask(GetDisposedTask<bool>());
        }
        else if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
        {
            result = ValueTaskFactory<bool>.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
        }
        else if (token.IsCancellationRequested)
        {
            result = ValueTaskFactory<bool>.FromCanceled(token);
        }
        else if (TryAcquire(ref manager))
        {
            result = ValueTaskFactory<bool>.FromResult<BooleanConst.True>();
        }
        else if (timeout == TimeSpan.Zero)
        {
            result = ValueTaskFactory<bool>.FromResult<BooleanConst.False>();    // if timeout is zero fail fast
        }
        else
        {
            result = ValueTaskFactory<bool>.FromSource(EnqueueNode(ref pool, ref manager, throwOnTimeout: false, callerInfo));
        }

        return result;
    }

    /// <summary>
    /// Cancels all suspended callers.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not in canceled state.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void CancelSuspendedCallers(CancellationToken token)
    {
        ThrowIfDisposed();

        if (!token.IsCancellationRequested)
            throw new ArgumentOutOfRangeException(nameof(token));

        unsafe
        {
            DrainWaitQueue(&TrySetCanceled, token);
        }

        static bool TrySetCanceled(LinkedValueTaskCompletionSource<bool> source, CancellationToken token)
            => source.TrySetCanceled(Sentinel.Instance, token);
    }

    private protected long ResumeSuspendedCallers()
    {
        unsafe
        {
            return DrainWaitQueue(&TrySetResult, arg: true);
        }

        static bool TrySetResult(LinkedValueTaskCompletionSource<bool> source, bool result)
            => source.TrySetResult(Sentinel.Instance, result);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void NotifyObjectDisposed()
    {
        var e = new ObjectDisposedException(GetType().Name);

        unsafe
        {
            DrainWaitQueue(&TrySetException, e);
        }

        static bool TrySetException(LinkedValueTaskCompletionSource<bool> source, ObjectDisposedException e)
            => source.TrySetException(Sentinel.Instance, e);
    }

    private unsafe long DrainWaitQueue<T>(delegate*<LinkedValueTaskCompletionSource<bool>, T, bool> callback, T arg)
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(callback != null);

        var count = 0L;

        for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();

            if (callback(current, arg))
                count++;
        }

        first = last = null;
        return count;
    }

    /// <summary>
    /// Releases all resources associated with exclusive lock.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe and may not be used concurrently with other members of this instance.
    /// </remarks>
    /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        IsDisposeRequested = true;

        if (disposing)
        {
            NotifyObjectDisposed();
            disposeTask.TrySetResult();
            callerInfo?.Dispose();
        }

        base.Dispose(disposing);
    }

    private protected virtual bool IsReadyToDispose => true;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected override ValueTask DisposeAsyncCore()
    {
        IsDisposeRequested = true;

        if (IsReadyToDispose)
        {
            Dispose(true);
            return ValueTask.CompletedTask;
        }

        return new(disposeTask.Task);
    }

    /// <summary>
    /// Disposes this synchronization primitive gracefully.
    /// </summary>
    /// <returns>The task representing asynchronous result.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}