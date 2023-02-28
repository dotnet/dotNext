using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;
using Timestamp = Diagnostics.Timestamp;

/// <summary>
/// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
/// </summary>
public class QueuedSynchronizer : Disposable
{
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

        if (first is null && manager.IsLockAllowed)
        {
            manager.AcquireLock();
            return true;
        }

        return false;
    }

    private protected ISupplier<TimeSpan, CancellationToken, TResult> GetTaskFactory<TNode, TLockManager, TResult>(ref TLockManager manager, ref ValueTaskPool<bool, TNode, Action<TNode>> pool)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
        where TResult : struct, IEquatable<TResult>
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(typeof(TResult).IsOneOf(typeof(ValueTask), typeof(ValueTask<bool>)));

        return IsDisposingOrDisposed
            ? GetDisposedTaskFactory<TResult>()
            : TryAcquire(ref manager)
            ? GetSuccessfulTaskFactory<TResult>()
            : Unsafe.As<ISupplier<TimeSpan, CancellationToken, TResult>>(EnqueueNode<TNode, TLockManager>(ref pool, ref manager, typeof(TResult) == typeof(ValueTask)));
    }

    // allocates but this is fine for this very uncommon situation
    private protected ISupplier<TimeSpan, CancellationToken, TResult> GetDisposedTaskFactory<TResult>()
        where TResult : struct, IEquatable<TResult>
    {
        Debug.Assert(typeof(TResult).IsOneOf(typeof(ValueTask), typeof(ValueTask<bool>)));

        return Unsafe.As<ISupplier<TimeSpan, CancellationToken, TResult>>(new WrapperTaskFactory(GetDisposedTask<bool>()));
    }

    internal static ISupplier<TimeSpan, CancellationToken, TResult> GetSuccessfulTaskFactory<TResult>()
        where TResult : struct, IEquatable<TResult>
    {
        Debug.Assert(typeof(TResult).IsOneOf(typeof(ValueTask), typeof(ValueTask<bool>)));

        return Unsafe.As<ISupplier<TimeSpan, CancellationToken, TResult>>(SuccessfulTaskFactory.Instance);
    }

    internal static ISupplier<TimeSpan, CancellationToken, TResult> GetTimedOutTaskFactory<TResult>()
        where TResult : struct, IEquatable<TResult>
    {
        Debug.Assert(typeof(TResult).IsOneOf(typeof(ValueTask), typeof(ValueTask<bool>)));

        return Unsafe.As<ISupplier<TimeSpan, CancellationToken, TResult>>(TimedOutTaskFactory.Instance);
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

    private sealed class WrapperTaskFactory : ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>
    {
        private readonly Task<bool> task;

        internal WrapperTaskFactory(Task<bool> task)
        {
            Debug.Assert(task is not null);

            this.task = task;
        }

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => new(task);

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => new(task);
    }

    private sealed class SuccessfulTaskFactory : ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>
    {
        internal static readonly SuccessfulTaskFactory Instance = new();

        private SuccessfulTaskFactory()
        {
        }

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => new(true);

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.CompletedTask;
    }

    private sealed class TimedOutTaskFactory : ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>
    {
        internal static readonly TimedOutTaskFactory Instance = new();

        private TimedOutTaskFactory()
        {
        }

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => new(false);

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromException(new TimeoutException());
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
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TContext>())
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
    /// Tests whether the lock acquisition can be done successfully before calling <see cref="AcquireCore(TContext)"/>.
    /// </summary>
    /// <param name="context">The context associated with the suspended caller or supplied externally.</param>
    /// <returns><see langword="true"/> if acquisition is allowed; otherwise, <see langword="false"/>.</returns>
    protected abstract bool CanAcquire(TContext context);

    /// <summary>
    /// Modifies the internal state according to acquisition semantics.
    /// </summary>
    /// <remarks>
    /// By default, this method does nothing.
    /// </remarks>
    /// <param name="context">The context associated with the suspended caller or supplied externally.</param>
    protected virtual void AcquireCore(TContext context)
    {
    }

    /// <summary>
    /// Modifies the internal state according to release semantics.
    /// </summary>
    /// <remarks>
    /// This method is called by <see cref="Release(TContext)"/> method.
    /// </remarks>
    /// <param name="context">The context associated with the suspended caller or supplied externally.</param>
    protected virtual void ReleaseCore(TContext context)
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

            if (!CanAcquire(current.Context!))
                break;

            if (RemoveAndSignal(current))
                AcquireCore(current.Context!);
        }
    }

    private protected sealed override bool IsReadyToDispose => first is null;

    /// <summary>
    /// Implements release semantics: attempts to resume the suspended callers.
    /// </summary>
    /// <remarks>
    /// This method doesn't invoke <see cref="ReleaseCore(TContext)"/> method and trying to resume
    /// suspended callers.
    /// </remarks>
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
    /// <remarks>
    /// This methods invokes <se cref="ReleaseCore(TContext)"/> to modify the internal state
    /// before resuming all suspended callers.
    /// </remarks>
    /// <param name="context">The argument to be passed to <see cref="ReleaseCore(TContext)"/>.</param>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void Release(TContext context)
    {
        ThrowIfDisposed();
        ReleaseCore(context);
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state synchronously.
    /// </summary>
    /// <remarks>
    /// This method invokes <see cref="CanAcquire(TContext)"/>, and if it returns <see langword="true"/>,
    /// invokes <see cref="AcquireCore(TContext)"/> to modify the internal state.
    /// </remarks>
    /// <param name="context">The context to be passed to <see cref="CanAcquire(TContext)"/>.</param>
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

        if (first is null && CanAcquire(context))
        {
            AcquireCore(context);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ISupplier<TimeSpan, CancellationToken, TResult> AcquireCore<TResult>(TContext context)
        where TResult : struct, IEquatable<TResult>
    {
        Debug.Assert(typeof(TResult).IsOneOf(typeof(ValueTask), typeof(ValueTask<bool>)));

        return IsDisposingOrDisposed
            ? GetDisposedTaskFactory<TResult>()
            : TryAcquireCore(context)
            ? GetSuccessfulTaskFactory<TResult>()
            : Unsafe.As<ISupplier<TimeSpan, CancellationToken, TResult>>(EnqueueNode(context, typeof(TResult) == typeof(ValueTask)));
    }

    private WaitNode EnqueueNode(TContext context, bool throwOnTimeout)
    {
        Debug.Assert(Monitor.IsEntered(this));

        var node = pool.Get();
        node.Context = context;
        node.Initialize(this, throwOnTimeout);
        EnqueueNode(node);
        return node;
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state asynchronously.
    /// </summary>
    /// <param name="context">The context to be passed to <see cref="CanAcquire(TContext)"/>.</param>
    /// <param name="timeout">The time to wait for the acquisition.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if acquisition is successful; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    protected ValueTask<bool> TryAcquireAsync(TContext context, TimeSpan timeout, CancellationToken token)
    {
        ValueTask<bool> task;

        switch (timeout.Ticks)
        {
            case < 0L and not Timeout.InfiniteTicks:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    task = new(TryAcquire(context));
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<bool>(e);
                }

                break;
            default:
                task = token.IsCancellationRequested
                    ? ValueTask.FromCanceled<bool>(token)
                    : AcquireCore<ValueTask<bool>>(context).Invoke(timeout, token);
                break;
        }

        return task;
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state asynchronously.
    /// </summary>
    /// <param name="context">The context to be passed to <see cref="CanAcquire(TContext)"/>.</param>
    /// <param name="timeout">The time to wait for the acquisition.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if acquisition is successful; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="TimeoutException">The operation cannot be completed within the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    protected ValueTask AcquireAsync(TContext context, TimeSpan timeout, CancellationToken token)
    {
        ValueTask task;

        switch (timeout.Ticks)
        {
            case < 0L and not Timeout.InfiniteTicks:
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    task = TryAcquire(context)
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(new TimeoutException());
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }

                break;
            default:
                task = token.IsCancellationRequested
                    ? ValueTask.FromCanceled(token)
                    : AcquireCore<ValueTask>(context).Invoke(timeout, token);
                break;
        }

        return task;
    }

    /// <summary>
    /// Implements acquire semantics: attempts to move this object to acquired state asynchronously.
    /// </summary>
    /// <param name="context">The context to be passed to <see cref="CanAcquire(TContext)"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    protected ValueTask AcquireAsync(TContext context, CancellationToken token)
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : AcquireCore<ValueTask>(context).Invoke(token);
}