using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

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
        // WaitNode - completion source
        private readonly object? result;
        private readonly CancellationToken token;
        private readonly TimeSpan timeout;

        private ValueTaskFactory(Task task)
        {
            Debug.Assert(task is { IsCompleted: true });

            result = task;
            token = default;
            timeout = default;
        }

        private ValueTaskFactory(WaitNode source, TimeSpan timeout, CancellationToken token)
        {
            Debug.Assert(source is not null);

            result = source;
            this.token = token;
            this.timeout = timeout;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask Create()
        {
            Debug.Assert(result is null or Task or WaitNode);

            return result switch
            {
                null => ValueTask.CompletedTask,
                WaitNode node => node.CreateVoidTask(timeout, token),
                object task => new(Unsafe.As<Task>(task)),
            };
        }

        internal static ValueTaskFactory FromCanceled(CancellationToken token)
            => new(Task.FromCanceled(token));

        internal static ValueTaskFactory FromException(Exception e)
            => new(Task.FromException(e));

        internal static ValueTaskFactory FromTask(Task t)
            => new(t);

        internal static ValueTaskFactory Completed => default;

        internal static ValueTaskFactory FromSource(WaitNode source, TimeSpan timeout, CancellationToken token)
            => new(source, timeout, token);
    }

    // This type allows to create the task out of the lock to reduce lock contention
    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct BooleanValueTaskFactory
    {
        // null - false
        // Sentinel.Instance - true
        // Task<bool> - completed task
        // ValueTaskCompletionSource<bool> - completion source
        private readonly object? result;
        private readonly CancellationToken token;
        private readonly TimeSpan timeout;

        public BooleanValueTaskFactory()
        {
            result = Sentinel.Instance;
            token = default;
            timeout = default;
        }

        private BooleanValueTaskFactory(Task<bool> task)
        {
            Debug.Assert(task is { IsCompleted: true });

            result = task;
            token = default;
            timeout = default;
        }

        private BooleanValueTaskFactory(ValueTaskCompletionSource<bool> source, TimeSpan timeout, CancellationToken token)
        {
            Debug.Assert(source is not null);

            result = source;
            this.token = token;
            this.timeout = timeout;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<bool> Create()
        {
            Debug.Assert(result is null or Task<bool> or ValueTaskCompletionSource<bool> || ReferenceEquals(result, Sentinel.Instance));

            return result switch
            {
                null => new(false),
                object sentinel when ReferenceEquals(sentinel, Sentinel.Instance) => new(true),
                ValueTaskCompletionSource<bool> node => node.CreateTask(timeout, token),
                object task => new(Unsafe.As<Task<bool>>(task)),
            };
        }

        internal static BooleanValueTaskFactory FromCanceled(CancellationToken token)
            => new(Task.FromCanceled<bool>(token));

        internal static BooleanValueTaskFactory FromException(Exception e)
            => new(Task.FromException<bool>(e));

        internal static BooleanValueTaskFactory FromSource(ValueTaskCompletionSource<bool> source, TimeSpan timeout, CancellationToken token)
            => new(source, timeout, token);

        internal static BooleanValueTaskFactory True => new();

        internal static BooleanValueTaskFactory False => default;

        internal static BooleanValueTaskFactory FromTask(Task<bool> task)
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

    private protected ValueTaskFactory WaitWithTimeout<TNode, TLockManager>(ref TLockManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TimeSpan timeout, CancellationToken token)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        ValueTaskFactory result;
        var callerInfo = this.callerInfo?.Capture();

        if (IsDisposingOrDisposed)
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
            result = ValueTaskFactory.FromSource(EnqueueNode(ref pool, ref manager, throwOnTimeout: true, callerInfo), timeout, token);
        }

        return result;
    }

    // optimized version without timeout support
    private protected ValueTaskFactory WaitNoTimeout<TNode, TLockManager>(ref TLockManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, CancellationToken token)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        ValueTaskFactory result;
        var callerInfo = this.callerInfo?.Capture();

        if (IsDisposingOrDisposed)
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
            result = ValueTaskFactory.FromSource(EnqueueNode(ref pool, ref manager, throwOnTimeout: false, callerInfo), InfiniteTimeSpan, token);
        }

        return result;
    }

    private protected BooleanValueTaskFactory WaitNoTimeout<TNode, TManager>(ref TManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TimeSpan timeout, CancellationToken token)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        BooleanValueTaskFactory result;
        var callerInfo = this.callerInfo?.Capture();

        if (IsDisposingOrDisposed)
        {
            result = BooleanValueTaskFactory.FromTask(GetDisposedTask<bool>());
        }
        else if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
        {
            result = BooleanValueTaskFactory.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
        }
        else if (token.IsCancellationRequested)
        {
            result = BooleanValueTaskFactory.FromCanceled(token);
        }
        else if (TryAcquire(ref manager))
        {
            result = BooleanValueTaskFactory.True;
        }
        else if (timeout == TimeSpan.Zero)
        {
            result = BooleanValueTaskFactory.False;    // if timeout is zero fail fast
        }
        else
        {
            result = BooleanValueTaskFactory.FromSource(EnqueueNode(ref pool, ref manager, throwOnTimeout: false, callerInfo), timeout, token);
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
            DrainWaitQueue(first, &TrySetCanceled, token);
        }

        first = last = null;

        static bool TrySetCanceled(LinkedValueTaskCompletionSource<bool> source, CancellationToken token)
            => source.TrySetCanceled(Sentinel.Instance, token);
    }

    private protected static long ResumeAll(LinkedValueTaskCompletionSource<bool>? head)
    {
        unsafe
        {
            return DrainWaitQueue(head, &TrySetResult, arg: true);
        }

        static bool TrySetResult(LinkedValueTaskCompletionSource<bool> source, bool result)
            => source.TrySetResult(Sentinel.Instance, result);
    }

    private protected LinkedValueTaskCompletionSource<bool>? DetachWaitQueue()
    {
        Monitor.IsEntered(this);

        var result = first;
        first = last = null;
        return result;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void NotifyObjectDisposed(Exception? reason = null)
    {
        reason ??= new ObjectDisposedException(GetType().Name);

        unsafe
        {
            DrainWaitQueue(first, &TrySetException, reason);
        }

        first = last = null;
    }

    private static bool TrySetException(LinkedValueTaskCompletionSource<bool> source, Exception reason)
        => source.TrySetException(Sentinel.Instance, reason);

    private static unsafe long DrainWaitQueue<T>(LinkedValueTaskCompletionSource<bool>? first, delegate*<LinkedValueTaskCompletionSource<bool>, T, bool> callback, T arg)
    {
        Debug.Assert(callback != null);

        var count = 0L;

        for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();

            if (callback(current, arg))
                count++;
        }

        return count;
    }

    private protected void Interrupt(object? reason)
    {
        Debug.Assert(Monitor.IsEntered(this));

        var e = new PendingTaskInterruptedException { Reason = reason };
        unsafe
        {
            DrainWaitQueue(first, &TrySetException, e);
        }

        first = last = null;
    }

    private void Dispose(bool disposing, Exception? reason)
    {
        if (disposing)
        {
            NotifyObjectDisposed(reason);
            disposeTask.TrySetResult();
            callerInfo?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Releases all resources associated with this object.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe and may not be used concurrently with other members of this instance.
    /// </remarks>
    /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
    protected override void Dispose(bool disposing)
        => Dispose(disposing, reason: null);

    /// <summary>
    /// Releases all resources associated with this object.
    /// </summary>
    /// <param name="reason">The exeption to be passed to all suspended callers.</param>
    public void Dispose(Exception? reason)
    {
        Dispose(TryBeginDispose(), reason);
        GC.SuppressFinalize(this);
    }

    private protected virtual bool IsReadyToDispose => true;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected override ValueTask DisposeAsyncCore()
    {
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