using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    private enum ActivationStrategy : byte
    {
        Completed = 0,
        Lazy,
        Error,
        Task,
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct ValueTaskFactory
    {
        private readonly ActivationStrategy status;
        private readonly object? result;

        internal ValueTaskFactory(Task task)
        {
            Debug.Assert(task is not null);

            result = task;
            status = ActivationStrategy.Task;
        }

        internal ValueTaskFactory(LinkedValueTaskCompletionSource<bool> source)
        {
            Debug.Assert(source is not null);

            result = source;
            status = ActivationStrategy.Lazy;
        }

        internal ValueTaskFactory(bool result)
        {
            this.result = result ? Sentinel.Instance : null;
            status = ActivationStrategy.Completed;
        }

        internal ValueTaskFactory(Exception e)
        {
            Debug.Assert(e is not null);

            result = e;
            status = ActivationStrategy.Error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask CreateVoidTask(TimeSpan timeout, CancellationToken token) => status switch
        {
            ActivationStrategy.Completed => ValueTask.CompletedTask,
            ActivationStrategy.Task => new(Unsafe.As<Task>(result!)),
            ActivationStrategy.Lazy => Unsafe.As<LinkedValueTaskCompletionSource<bool>>(result!).CreateVoidTask(timeout, token),
            ActivationStrategy.Error => ValueTask.FromException(Unsafe.As<Exception>(result!)),
            _ => ValueTask.FromException(new SwitchExpressionException()),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask CreateVoidTask(CancellationToken token) => CreateVoidTask(InfiniteTimeSpan, token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<bool> CreateTask(TimeSpan timeout, CancellationToken token) => status switch
        {
            ActivationStrategy.Completed => new(result is not null),
            ActivationStrategy.Task => new(Unsafe.As<Task<bool>>(result!)),
            ActivationStrategy.Lazy => Unsafe.As<LinkedValueTaskCompletionSource<bool>>(result!).CreateTask(timeout, token),
            ActivationStrategy.Error => ValueTask.FromException<bool>(Unsafe.As<Exception>(result!)),
            _ => ValueTask.FromException<bool>(new SwitchExpressionException()),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<bool> CreateTask(CancellationToken token) => CreateTask(InfiniteTimeSpan, token);
    }

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
        private readonly WeakReference<QueuedSynchronizer?> owner = new(target: null, trackResurrection: false);
        private Timestamp createdAt;
        private bool throwOnTimeout;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo
        {
            get;
            private set;
        }

        private protected override void ResetCore()
        {
            owner.SetTarget(target: null);
            CallerInfo = null;
            base.ResetCore();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(QueuedSynchronizer owner, bool throwOnTimeout)
        {
            Debug.Assert(owner is not null);

            this.throwOnTimeout = throwOnTimeout;
            Initialize(owner);
        }

        internal void Initialize(QueuedSynchronizer owner)
        {
            Debug.Assert(owner is not null);

            this.owner.SetTarget(owner);
            CallerInfo = owner.callerInfo?.Capture();
            createdAt = new();
        }

        protected sealed override Result<bool> OnTimeout() => throwOnTimeout ? base.OnTimeout() : false;

        private protected static void AfterConsumed<T>(T node)
            where T : WaitNode, IPooledManualResetCompletionSource<Action<T>>
        {
            // report lock duration
            if (node.owner.TryGetTarget(out var owner))
            {
                var duration = node.createdAt.ElapsedMilliseconds;
                owner.lockDurationCounter?.Invoke(duration);
                LockDurationMeter.Record(duration, owner.measurementTags);
            }

            node.As<IPooledManualResetCompletionSource<Action<T>>>().OnConsumed?.Invoke(node);
        }
    }

    private protected sealed class DefaultWaitNode : WaitNode, IPooledManualResetCompletionSource<Action<DefaultWaitNode>>
    {
        protected sealed override void AfterConsumed() => AfterConsumed(this);

        Action<DefaultWaitNode>? IPooledManualResetCompletionSource<Action<DefaultWaitNode>>.OnConsumed { get; set; }
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

    private const string LockTypeMeterAttribute = "dotnext.threading.asynclock.type";
    private static readonly Counter<int> LockContentionMeter;
    private static readonly Histogram<double> LockDurationMeter;

    private readonly Action<double>? contentionCounter, lockDurationCounter;
    private readonly TagList measurementTags;
    private readonly TaskCompletionSource disposeTask;
    private CallerInformationStorage? callerInfo;
    private protected LinkedValueTaskCompletionSource<bool>? first;
    private LinkedValueTaskCompletionSource<bool>? last;

    static QueuedSynchronizer()
    {
        var meter = new Meter("DotNext.Threading.AsyncLock");
        LockContentionMeter = meter.CreateCounter<int>("async-lock-contention-count", description: "Async Lock Contention");
        LockDurationMeter = meter.CreateHistogram<double>("async-lock-duration", unit: "ms", description: "Async Lock Duration");
    }

    private protected QueuedSynchronizer()
    {
        disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        measurementTags = new() { { LockTypeMeterAttribute, GetType().Name } };
    }

    /// <summary>
    /// Sets a list of tags to be associated with each measurement.
    /// </summary>
    [CLSCompliant(false)]
    public TagList MeasurementTags
    {
        init
        {
            value.Add(LockTypeMeterAttribute, GetType().Name);
            measurementTags = value;
        }
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
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IReadOnlyList<object?> GetSuspendedCallers()
        => callerInfo is null ? Array.Empty<object?>() : GetSuspendedCallersCore();

    /// <summary>
    /// Sets counter for lock contention.
    /// </summary>
    [Obsolete("Use System.Diagnostics.Metrics infrastructure instead.", UrlFormat = "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics")]
    public IncrementingEventCounter? LockContentionCounter
    {
        init => contentionCounter = value is not null ? value.Increment : null;
    }

    /// <summary>
    /// Sets counter of lock duration, in milliseconds.
    /// </summary>
    [Obsolete("Use System.Diagnostics.Metrics infrastructure instead.", UrlFormat = "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics")]
    public EventCounter? LockDurationCounter
    {
        init => lockDurationCounter = value is not null ? value.WriteMetric : null;
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

    private protected void EnqueueNode(WaitNode freshNode)
    {
        if (last is null)
        {
            first = last = freshNode;
        }
        else
        {
            last.Append(freshNode);
            last = freshNode;
        }

        contentionCounter?.Invoke(1D);
        LockContentionMeter.Add(1, measurementTags);
    }

    private TNode EnqueueNode<TNode, TLockManager>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, bool throwOnTimeout)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        var node = pool.Get();
        manager.InitializeNode(node);
        node.Initialize(this, throwOnTimeout);
        EnqueueNode(node);
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

    private protected static bool ValidateTimeoutAndToken(TimeSpan timeout, CancellationToken token, out ValueTask result)
    {
        if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
        {
            result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
        }
        else if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            Unsafe.SkipInit(out result);
            return true;
        }

        return false;
    }

    internal static bool ValidateTimeoutAndToken(TimeSpan timeout, CancellationToken token, out ValueTask<bool> result)
    {
        if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
        {
            result = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
        }
        else if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<bool>(token);
        }
        else
        {
            Unsafe.SkipInit(out result);
            return true;
        }

        return false;
    }

    private protected ValueTaskFactory Wait<TNode, TLockManager>(ref TLockManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool, bool throwOnTimeout, bool zeroTimeout)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        ValueTaskFactory result;

        if (IsDisposingOrDisposed)
        {
            result = throwOnTimeout ? new(DisposedTask) : new(GetDisposedTask<bool>());
        }
        else if (TryAcquire(ref manager))
        {
            result = new(true);
        }
        else if (zeroTimeout)
        {
            result = throwOnTimeout ? new(new TimeoutException()) : new(false);
        }
        else
        {
            result = new(EnqueueNode(ref pool, ref manager, throwOnTimeout));
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

        first?.TrySetCanceledAndSentinelToAll(token);
        first = last = null;
    }

    private protected static long ResumeAll(LinkedValueTaskCompletionSource<bool>? head)
        => head?.TrySetResultAndSentinelToAll(result: true) ?? 0L;

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
        first?.TrySetExceptionAndSentinelToAll(reason ?? new ObjectDisposedException(GetType().Name));
        first = last = null;
    }

    private protected void Interrupt(object? reason)
    {
        Debug.Assert(Monitor.IsEntered(this));

        first?.TrySetExceptionAndSentinelToAll(new PendingTaskInterruptedException { Reason = reason });
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
        if (this is { IsReadyToDispose: true, IsDisposed: false })
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

/// <summary>
/// Provides low-level infrastructure for writing custom synchronization primitives.
/// </summary>
/// <typeparam name="TContext">The context to be associated with each suspended caller.</typeparam>
public abstract class QueuedSynchronizer<TContext> : QueuedSynchronizer
{
    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<Action<WaitNode>>
    {
        internal TContext? Context;

        protected override void AfterConsumed() => AfterConsumed(this);

        private protected override void ResetCore()
        {
            Context = default;
            base.ResetCore();
        }

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed { get; set; }
    }

    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;

    /// <summary>
    /// Initializes a new synchronization primitive.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is not <see langword="null"/> and less than 1.</exception>
    protected QueuedSynchronizer(int? concurrencyLevel)
    {
        pool = concurrencyLevel switch
        {
            null => new(OnCompleted),
            { } value when value > 0 => new(OnCompleted, value),
            _ => throw new ArgumentOutOfRangeException(nameof(concurrencyLevel)),
        };
    }

    /// <summary>
    /// Tests whether the lock state can be changed.
    /// </summary>
    /// <param name="context">The context associated with the suspended caller or supplied externally.</param>
    /// <returns><see langword="true"/> if transition is allowed; otherwise, <see langword="false"/>.</returns>
    protected abstract bool Test(TContext context);

    /// <summary>
    /// Modifies the internal state of the synchronization primitive.
    /// </summary>
    /// <param name="context">The context associated with the suspended caller or supplied externally.</param>
    protected virtual void Transit(TContext context)
    {
    }

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    private void OnCompleted(WaitNode node)
    {
        if (node.NeedsRemoval && RemoveNode(node))
            DrainWaitQueue();

        pool.Return(node);
    }

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(first is null or WaitNode);

        for (WaitNode? current = Unsafe.As<WaitNode>(first), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or WaitNode);

            next = Unsafe.As<WaitNode>(current.Next);

            if (current.IsCompleted)
            {
                RemoveNode(current);
                continue;
            }

            if (!Test(current.Context!))
                break;

            if (RemoveAndSignal(current))
                Transit(current.Context!);
        }
    }

    private protected sealed override bool IsReadyToDispose => first is null;

    /// <summary>
    /// Implements release semantics: attempts to resume the suspended callers.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void Release()
    {
        ThrowIfDisposed();
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    /// <summary>
    /// Implements release semantics: attempts to resume the suspended callers.
    /// </summary>
    /// <typeparam name="TAction">The type of the action implementing state transition logic.</typeparam>
    /// <typeparam name="T">The type of the argument to be passed to <paramref name="transition"/>.</typeparam>
    /// <param name="transition">The action that can be used to transform the internal state of this primitive.</param>
    /// <param name="arg">The argument to be passed to <paramref name="transition"/>.</param>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void Release<TAction, T>(TAction transition, T arg)
        where TAction : notnull, IConsumer<T>
    {
        ThrowIfDisposed();
        transition.Invoke(arg);
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state synchronously.
    /// </summary>
    /// <remarks>
    /// This method invokes <see cref="Test(TContext)"/>, and if it returns <see langword="true"/>,
    /// invokes <see cref="Transit(TContext)"/> to modify the internal state.
    /// </remarks>
    /// <param name="context">The context to be passed to <see cref="Test(TContext)"/>.</param>
    /// <returns><see langword="true"/> if this primitive is in acquired state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected bool TryAcquire(TContext context)
    {
        ThrowIfDisposed();

        return TryAcquireCore(context);
    }

    private bool TryAcquireCore(TContext context)
    {
        Debug.Assert(Monitor.IsEntered(this));

        bool result;

        if (result = Test(context))
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

            Transit(context);
        }

    exit:
        return result;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory Acquire(TContext context, bool zeroTimeout)
    {
        ValueTaskFactory result;

        if (IsDisposingOrDisposed)
        {
            result = new(GetDisposedTask<bool>());
        }
        else if (TryAcquireCore(context))
        {
            result = new(true);
        }
        else if (zeroTimeout)
        {
            result = new(false);
        }
        else
        {
            result = new(EnqueueNode(context));
        }

        return result;
    }

    private WaitNode EnqueueNode(TContext context)
    {
        Debug.Assert(Monitor.IsEntered(this));

        var node = pool.Get();
        node.Context = context;
        node.Initialize(this);
        EnqueueNode(node);
        return node;
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state asynchronously.
    /// </summary>
    /// <param name="context">The context to be passed to <see cref="Test(TContext)"/>.</param>
    /// <param name="timeout">The time to wait for the acquisition.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if acquisition is successful; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    protected ValueTask<bool> AcquireAsync(TContext context, TimeSpan timeout, CancellationToken token)
    {
        ValueTask<bool> task;

        if (ValidateTimeoutAndToken(timeout, token, out task))
        {
            task = Acquire(context, timeout == TimeSpan.Zero).CreateTask(timeout, token);
        }

        return task;
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state asynchronously.
    /// </summary>
    /// <param name="context">The context to be passed to <see cref="Test(TContext)"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    protected ValueTask AcquireAsync(TContext context, CancellationToken token)
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : Acquire(context, zeroTimeout: false).CreateVoidTask(token);
}