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

    private TNode EnqueueNode<TNode, TLockManager>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, bool throwOnTimeout)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
    {
        Debug.Assert(Monitor.IsEntered(this));

        var node = pool.Get();
        manager.InitializeNode(node);
        node.Initialize(this, throwOnTimeout);

        if (last is null)
        {
            first = last = node;
        }
        else
        {
            last.Append(node);
            last = node;
        }

        contentionCounter?.Invoke(1D);
        LockContentionMeter.Add(1, measurementTags);
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