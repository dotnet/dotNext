using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;
using Timestamp = Diagnostics.Timestamp;

/// <summary>
/// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
/// </summary>
public class QueuedSynchronizer : Disposable
{
    private const string LockTypeMeterAttribute = "dotnext.asynclock.type";
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

    private protected object SyncRoot => disposeTask;

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

    private IReadOnlyList<object?> GetSuspendedCallersCore()
    {
        List<object?> list;
        lock (SyncRoot)
        {
            if (first is null)
                return Array.Empty<Activity?>();

            list = new List<object?>();
            for (LinkedValueTaskCompletionSource<bool>? current = first; current is not null; current = current.Next)
            {
                if (current is WaitNode node)
                    list.Add(node.CallerInfo);
            }
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
        Debug.Assert(Monitor.IsEntered(SyncRoot));

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

    private protected TNode EnqueueNode<TNode, TLockManager>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, bool throwOnTimeout)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var node = pool.Get();
        manager.InitializeNode(node);
        node.Initialize(this, throwOnTimeout);
        EnqueueNode(node);
        return node;
    }

    private protected bool TryAcquire<TLockManager>(ref TLockManager manager)
        where TLockManager : struct, ILockManager
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (first is null && manager.IsLockAllowed)
        {
            manager.AcquireLock();
            return true;
        }

        return false;
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
        where TOptions : struct, IAcquisitionOptions
    {
        ValueTask task;

        switch (options.Timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L:
                task = ValueTask.FromException(new ArgumentOutOfRangeException("timeout"));
                break;
            case 0L: // attempt to acquire synchronously
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(DisposedTask);
                        break;
                    }

#pragma warning disable CA2252
                    if (TOptions.InterruptionRequired)
#pragma warning restore CA2252
                        Interrupt(options.InterruptionReason);

                    task = TryAcquire(ref manager)
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(new TimeoutException());
                }

                break;
            default:
                if (options.Token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled(options.Token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(DisposedTask);
                        break;
                    }

#pragma warning disable CA2252
                    if (TOptions.InterruptionRequired)
#pragma warning restore CA2252
                        Interrupt(options.InterruptionReason);

                    if (TryAcquire(ref manager))
                    {
                        task = ValueTask.CompletedTask;
                        break;
                    }

                    factory = EnqueueNode(ref pool, ref manager, throwOnTimeout: true);
                }

                task = factory.Invoke(options.Timeout, options.Token);
                break;
        }

        return task;
    }

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
        where TOptions : struct, IAcquisitionOptions
    {
        ValueTask<bool> task;

        switch (options.Timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException("timeout"));
                break;
            case 0L: // attempt to acquire synchronously
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }
#pragma warning disable CA2252
                    if (TOptions.InterruptionRequired)
#pragma warning restore CA2252
                        Interrupt(options.InterruptionReason);

                    task = new(TryAcquire(ref manager));
                }

                break;
            default:
                if (options.Token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled<bool>(options.Token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

#pragma warning disable CA2252
                    if (TOptions.InterruptionRequired)
#pragma warning restore CA2252
                        Interrupt(options.InterruptionReason);

                    if (TryAcquire(ref manager))
                    {
                        task = new(true);
                        break;
                    }

                    factory = EnqueueNode(ref pool, ref manager, throwOnTimeout: false);
                }

                task = factory.Invoke(options.Timeout, options.Token);
                break;
        }

        return task;
    }

    /// <summary>
    /// Cancels all suspended callers.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not in canceled state.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void CancelSuspendedCallers(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
            throw new ArgumentOutOfRangeException(nameof(token));

        lock (SyncRoot)
        {
            ThrowIfDisposed();
            first?.TrySetCanceledAndSentinelToAll(token);
            first = last = null;
        }
    }

    private protected static long ResumeAll(LinkedValueTaskCompletionSource<bool>? head)
        => head?.TrySetResultAndSentinelToAll(result: true) ?? 0L;

    private protected LinkedValueTaskCompletionSource<bool>? DetachWaitQueue()
    {
        Monitor.IsEntered(SyncRoot);

        var result = first;
        first = last = null;
        return result;
    }

    private protected LinkedValueTaskCompletionSource<bool>? DetachHead()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (first is { } head)
        {
            RemoveNode(head);
        }
        else
        {
            head = null;
        }

        return head;
    }

    private void NotifyObjectDisposed(Exception? reason = null)
    {
        reason ??= new ObjectDisposedException(GetType().Name);

        lock (SyncRoot)
        {
            first?.TrySetExceptionAndSentinelToAll(reason);
            first = last = null;
        }
    }

    private void Interrupt(object? reason)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

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
    protected override ValueTask DisposeAsyncCore()
    {
        ValueTask result;

        lock (SyncRoot)
        {
            if (this is { IsReadyToDispose: true, IsDisposed: false })
            {
                Dispose(true);
                result = ValueTask.CompletedTask;
            }
            else
            {
                result = new(disposeTask.Task);
            }
        }

        return result;
    }

    /// <summary>
    /// Disposes this synchronization primitive gracefully.
    /// </summary>
    /// <returns>The task representing asynchronous result.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();

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

        protected override void Cleanup()
        {
            owner.SetTarget(target: null);
            CallerInfo = null;
            base.Cleanup();
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

            node.OnConsumed?.Invoke(node);
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

    /// <summary>
    /// Represents acquisition options.
    /// </summary>
    private protected interface IAcquisitionOptions
    {
        CancellationToken Token { get; }

        TimeSpan Timeout { get; }

        object? InterruptionReason { get; }

        [RequiresPreviewFeatures]
        static abstract bool InterruptionRequired { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct CancellationTokenOnly : IAcquisitionOptions
    {
        internal CancellationTokenOnly(CancellationToken token) => Token = token;

        public CancellationToken Token { get; }

        [RequiresPreviewFeatures]
        static bool IAcquisitionOptions.InterruptionRequired => false;

        object? IAcquisitionOptions.InterruptionReason => null;

        TimeSpan IAcquisitionOptions.Timeout => new(Timeout.InfiniteTicks);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct TimeoutAndCancellationToken : IAcquisitionOptions
    {
        internal TimeoutAndCancellationToken(TimeSpan timeout, CancellationToken token)
        {
            Timeout = timeout;
            Token = token;
        }

        public CancellationToken Token { get; }

        public TimeSpan Timeout { get; }

        [RequiresPreviewFeatures]
        static bool IAcquisitionOptions.InterruptionRequired => false;

        object? IAcquisitionOptions.InterruptionReason => null;
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct InterruptionReasonAndCancellationToken : IAcquisitionOptions
    {
        internal InterruptionReasonAndCancellationToken(object? reason, CancellationToken token)
        {
            InterruptionReason = reason;
            Token = token;
        }

        public CancellationToken Token { get; }

        public object? InterruptionReason { get; }

        [RequiresPreviewFeatures]
        static bool IAcquisitionOptions.InterruptionRequired => true;

        TimeSpan IAcquisitionOptions.Timeout => new(Timeout.InfiniteTicks);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct TimeoutAndInterruptionReasonAndCancellationToken : IAcquisitionOptions
    {
        internal TimeoutAndInterruptionReasonAndCancellationToken(object? reason, TimeSpan timeout, CancellationToken token)
        {
            InterruptionReason = reason;
            Timeout = timeout;
            Token = token;
        }

        public CancellationToken Token { get; }

        public object? InterruptionReason { get; }

        [RequiresPreviewFeatures]
        static bool IAcquisitionOptions.InterruptionRequired => true;

        public TimeSpan Timeout { get; }
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

        protected override void Cleanup()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TContext>())
                Context = default;

            base.Cleanup();
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

    private void OnCompleted(WaitNode node)
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval && RemoveNode(node))
                DrainWaitQueue();

            pool.Return(node);
        }
    }

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
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
    protected void Release()
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();
            DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }
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
    protected void Release(TContext context)
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();
            ReleaseCore(context);
            DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }
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
    protected bool TryAcquire(TContext context)
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();
            return TryAcquireCore(context);
        }
    }

    private bool TryAcquireCore(TContext context)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (first is null && CanAcquire(context))
        {
            AcquireCore(context);
            return true;
        }

        return false;
    }

    private WaitNode EnqueueNode(TContext context, bool throwOnTimeout)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

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
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                lock (SyncRoot)
                {
                    task = IsDisposingOrDisposed
                        ? new(GetDisposedTask<bool>())
                        : new(TryAcquireCore(context));
                }

                break;
            default:
                if (token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled<bool>(token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

                    if (TryAcquireCore(context))
                    {
                        task = new(true);
                        break;
                    }

                    factory = EnqueueNode(context, throwOnTimeout: false);
                }

                task = factory.Invoke(timeout, token);
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
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L:
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                lock (SyncRoot)
                {
                    task = IsDisposingOrDisposed
                        ? new(GetDisposedTask<bool>())
                        : TryAcquireCore(context)
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(new TimeoutException());
                }

                break;
            default:
                if (token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled(token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

                    if (TryAcquireCore(context))
                    {
                        task = ValueTask.CompletedTask;
                        break;
                    }

                    factory = EnqueueNode(context, throwOnTimeout: true);
                }

                task = factory.Invoke(timeout, token);
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
        => AcquireAsync(context, new(Timeout.InfiniteTicks), token);
}