using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
/// </summary>
/// <remarks>
/// This class is designed to provide better throughput rather than optimized response time.
/// It means that it minimizes contention between concurrent calls and allows to process
/// as many concurrent requests as possible by the cost of the execution time of a single
/// method for a particular caller.
/// </remarks>
public class QueuedSynchronizer : Disposable
{
    private const string LockTypeMeterAttribute = "dotnext.asynclock.type";
    private static readonly UpDownCounter<int> SuspendedCallersMeter;

    private readonly TaskCompletionSource disposeTask;
    private CallerInformationStorage? callerInfo;
    private WaitQueue waitQueue;

    static QueuedSynchronizer()
    {
        var meter = new Meter("DotNext.Threading.AsyncLock");
        SuspendedCallersMeter = meter.CreateUpDownCounter<int>("suspended-callers-count", description: "Number of Suspended Callers");
    }

    private protected QueuedSynchronizer()
    {
        disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        waitQueue.MeasurementTags = new() { { LockTypeMeterAttribute, GetType().Name } };
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected bool IsEmptyQueue => waitQueue.Head is null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
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
            waitQueue.MeasurementTags = value;
        }
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

    private object? CaptureCallerInformation() => callerInfo?.Capture();

    private IReadOnlyList<object?> GetSuspendedCallersCore()
    {
        List<object?> list;
        lock (SyncRoot)
        {
            if (waitQueue.Head is null)
                return [];

            list = [];
            for (var current = waitQueue.Head; current is not null; current = current.Next)
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
        => callerInfo is null ? [] : GetSuspendedCallersCore();

    private protected void ReturnNode<TNode>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TNode node)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval)
                waitQueue.Remove(node);

            pool.Return(node);
        }
    }

    private protected void ReturnNode<TNode, TVisitor>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TNode node, ref TVisitor visitor)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TVisitor : struct, IWaitQueueVisitor<TNode>
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = node.NeedsRemoval && waitQueue.Remove(node)
                ? DrainWaitQueue<TNode, TVisitor>(ref visitor)
                : null;

            pool.Return(node);
        }

        suspendedCallers?.Unwind();
    }

    private protected LinkedValueTaskCompletionSource<bool>? DrainWaitQueue<TNode, TVisitor>(ref TVisitor visitor)
        where TNode : WaitNode
        where TVisitor : struct, IWaitQueueVisitor<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        for (TNode? current = Unsafe.As<TNode>(waitQueue.Head), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or TNode);

            next = Unsafe.As<TNode>(current.Next);

            if (!visitor.Visit(current, ref waitQueue, ref detachedQueue))
                goto exit;
        }
        
        visitor.EndOfQueueReached();

        exit:
        return detachedQueue.First;
    }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue(Exception e)
    {
        var visitor = new WaitQueueInterruptingVisitor(e);
        return DrainWaitQueue<WaitNode, WaitQueueInterruptingVisitor>(ref visitor);
    }

    private TNode EnqueueNode<TNode, TInitializer>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TInitializer initializer)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TInitializer : struct, INodeInitializer<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var node = pool.Get();
        initializer.Invoke(node);
        node.Initialize(CaptureCallerInformation(), initializer.Flags);
        waitQueue.Add(node);
        return node;
    }

    private protected TNode EnqueueNode<TNode, TLockManager>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, WaitNodeFlags flags)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
        => EnqueueNode<TNode, StaticInitializer<TNode, TLockManager>>(ref pool, new(flags));

    private protected bool TryAcquire<TLockManager>(ref TLockManager manager)
        where TLockManager : struct, ILockManager
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (TLockManager.RequiresEmptyQueue && !IsEmptyQueue || !manager.IsLockAllowed)
            return false;

        manager.AcquireLock();
        return true;
    }
    
    private T AcquireAsync<T, TNode, TInitializer, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TLockManager manager, TInitializer initializer, TOptions options)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, IValueTaskFactory<T>, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TInitializer : struct, INodeInitializer<TNode>
        where TLockManager : struct, ILockManager
        where TOptions : struct, IAcquisitionOptions
    {
        T task;

        switch (options.Timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L or > Timeout.MaxTimeoutParameterTicks:
                task = TNode.FromException(new ArgumentOutOfRangeException("timeout"));
                break;
            case 0L: // attempt to acquire synchronously
                LinkedValueTaskCompletionSource<bool>? interruptedCallers;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = TNode.FromException(CreateObjectDisposedException());
                        break;
                    }

                    interruptedCallers = TOptions.InterruptionRequired
                        ? Interrupt(options.InterruptionReason)
                        : null;

                    task = TryAcquire(ref manager)
                        ? TNode.SuccessfulTask
                        : TNode.TimedOutTask;
                }

                interruptedCallers?.Unwind();
                break;
            default:
                if (options.Token.IsCancellationRequested)
                {
                    task = TNode.FromCanceled(options.Token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, T> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = TNode.FromException(CreateObjectDisposedException());
                        break;
                    }

                    interruptedCallers = TOptions.InterruptionRequired
                        ? Interrupt(options.InterruptionReason)
                        : null;

                    if (TryAcquire(ref manager))
                    {
                        task = TNode.SuccessfulTask;
                        break;
                    }

                    factory = EnqueueNode(ref pool, initializer);
                }

                interruptedCallers?.Unwind();
                task = factory.Invoke(options.Timeout, options.Token);
                break;
        }

        return task;

        ObjectDisposedException CreateObjectDisposedException()
            => new(GetType().Name);
    }
    
    private protected ValueTask AcquireAsync<TNode, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager, TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
        where TOptions : struct, IAcquisitionOptions
        => AcquireAsync<ValueTask, TNode, StaticInitializer<TNode, TLockManager>, TLockManager, TOptions>(
            ref pool,
            ref manager,
            new(WaitNodeFlags.ThrowOnTimeout),
            options);

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager, TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager<TNode>
        where TOptions : struct, IAcquisitionOptionsWithTimeout
        => AcquireAsync<ValueTask<bool>, TNode, StaticInitializer<TNode, TLockManager>, TLockManager, TOptions>(
            ref pool,
            ref manager,
            new(WaitNodeFlags.None),
            options);
    
    private protected ValueTask AcquireSpecialAsync<TNode, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager, TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptions
        => AcquireAsync<ValueTask, TNode, NodeInitializer<TNode, TLockManager>, TLockManager, TOptions>(
            ref pool,
            ref manager,
            new(WaitNodeFlags.ThrowOnTimeout, ref manager),
            options);
    
    private protected ValueTask<bool> TryAcquireSpecialAsync<TNode, TLockManager, TOptions>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager, TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptionsWithTimeout
        => AcquireAsync<ValueTask<bool>, TNode, NodeInitializer<TNode, TLockManager>, TLockManager, TOptions>(
            ref pool,
            ref manager,
            new(WaitNodeFlags.None, ref manager),
            options);

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

        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        var exception = new OperationCanceledException(token);
        ExceptionDispatchInfo.SetCurrentStackTrace(exception);
        
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = DrainWaitQueue(exception);
        }

        suspendedCallers?.Unwind();
    }

    private void NotifyObjectDisposed(Exception? reason = null)
    {
        if (reason is null)
        {
            ExceptionDispatchInfo.SetCurrentStackTrace(reason = CreateException());
        }

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = DrainWaitQueue(reason);
        }

        suspendedCallers?.Unwind();
    }

    private protected LinkedValueTaskCompletionSource<bool>? Interrupt(object? reason = null)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        
        var exception = new PendingTaskInterruptedException { Reason = reason };
        ExceptionDispatchInfo.SetCurrentStackTrace(exception);

        return DrainWaitQueue(exception);
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
    /// <param name="reason">The exception to be passed to all suspended callers.</param>
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

    [Flags]
    internal enum WaitNodeFlags
    {
        None = 0,
        ThrowOnTimeout = 1,
    }
    
    private interface INodeInitializer<in TNode> : IConsumer<TNode>
        where TNode : WaitNode
    {
        WaitNodeFlags Flags { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct StaticInitializer<TNode, TLockManager>(WaitNodeFlags flags) : INodeInitializer<TNode>
        where TNode : WaitNode
        where TLockManager : struct, ILockManager<TNode>
    {
        WaitNodeFlags INodeInitializer<TNode>.Flags => flags;

        void IConsumer<TNode>.Invoke(TNode node) => TLockManager.InitializeNode(node);
    }

    // TODO: Replace with allows ref anti-constraint and ref struct
    [StructLayout(LayoutKind.Auto)]
    private readonly struct NodeInitializer<TNode, TLockManager>([ConstantExpected] WaitNodeFlags flags, ref TLockManager manager)
        : INodeInitializer<TNode>
        where TNode : WaitNode
        where TLockManager : struct, ILockManager, IConsumer<TNode>
    {
        private readonly unsafe void* managerOnStack = Unsafe.AsPointer(ref manager);

        WaitNodeFlags INodeInitializer<TNode>.Flags => flags;

        unsafe void IConsumer<TNode>.Invoke(TNode node)
            => Unsafe.AsRef<TLockManager>(managerOnStack).Invoke(node);
    }

    private interface IValueTaskFactory<out T> : ISupplier<TimeSpan, CancellationToken, T>
        where T : struct, IEquatable<T>
    {
        static abstract T SuccessfulTask { get; }
        
        static abstract T TimedOutTask { get; }

        static abstract T FromException(Exception e);

        static abstract T FromCanceled(CancellationToken token);
    }

    private protected abstract class WaitNode : LinkedValueTaskCompletionSource<bool>, IValueTaskFactory<ValueTask>,
        IValueTaskFactory<ValueTask<bool>>
    {
        private WaitNodeFlags flags;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo { get; private set; }

        protected override void CleanUp()
        {
            CallerInfo = null;
            base.CleanUp();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(object? callerInfo, WaitNodeFlags flags)
        {
            this.flags = flags;
            CallerInfo = callerInfo;
        }

        protected sealed override Result<bool> OnTimeout()
            => (flags & WaitNodeFlags.ThrowOnTimeout) is not 0 ? base.OnTimeout() : false;

        private protected static void AfterConsumed<T>(T node)
            where T : WaitNode, IPooledManualResetCompletionSource<Action<T>>
            => node.OnConsumed?.Invoke(node);

        static ValueTask IValueTaskFactory<ValueTask>.SuccessfulTask => ValueTask.CompletedTask;

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.SuccessfulTask => ValueTask.FromResult(true);

        static ValueTask IValueTaskFactory<ValueTask>.TimedOutTask => ValueTask.FromException(new TimeoutException());

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.TimedOutTask => ValueTask.FromResult(false);

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.FromException(Exception e)
            => ValueTask.FromException<bool>(e);

        static ValueTask IValueTaskFactory<ValueTask>.FromException(Exception e)
            => ValueTask.FromException(e);

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.FromCanceled(CancellationToken token)
            => ValueTask.FromCanceled<bool>(token);

        static ValueTask IValueTaskFactory<ValueTask>.FromCanceled(CancellationToken token)
            => ValueTask.FromCanceled(token);
    }

    private protected sealed class DefaultWaitNode : WaitNode, IPooledManualResetCompletionSource<Action<DefaultWaitNode>>
    {
        protected override void AfterConsumed() => AfterConsumed(this);

        Action<DefaultWaitNode>? IPooledManualResetCompletionSource<Action<DefaultWaitNode>>.OnConsumed { get; set; }
    }

    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();

        static virtual bool RequiresEmptyQueue => true;
    }

    private protected interface ILockManager<in TNode> : ILockManager
        where TNode : WaitNode
    {
        static virtual void InitializeNode(TNode node)
        {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct WaitQueue : IWaitQueue
    {
        internal TagList MeasurementTags;
        private LinkedValueTaskCompletionSource<bool>.LinkedList list;

        private bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue,
            in Result<bool> result)
        {
            bool signaled;
            if (!Remove(node))
            {
                signaled = false;
            }
            else if ((signaled = node.TrySetResult(Sentinel.Instance, completionToken: null, result, out var resumable)) && resumable)
            {
                detachedQueue.Add(node);
            }

            return signaled;
        }

        bool IWaitQueue.RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
            => RemoveAndSignal(node, ref detachedQueue, result: true);

        bool IWaitQueue.RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue,
            Exception e)
            => RemoveAndSignal(node, ref detachedQueue, new(e));

        public bool Remove(LinkedValueTaskCompletionSource<bool> node)
        {
            bool removed;
            if (removed = list.Remove(node))
            {
                SuspendedCallersMeter.Add(-1, MeasurementTags);
            }

            return removed;
        }

        public void Add(WaitNode node)
        {
            list.Add(node);
            SuspendedCallersMeter.Add(1, MeasurementTags);
        }

        public readonly LinkedValueTaskCompletionSource<bool>? Head => list.First;
    }

    private protected interface IWaitQueue
    {
        bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue);

        bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue,
            Exception e);
    }

    private protected interface IWaitQueueVisitor<in TNode>
        where TNode : WaitNode
    {
        bool Visit<TWaitQueue>(TNode node, ref TWaitQueue queue, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
            where TWaitQueue : IWaitQueue;

        void EndOfQueueReached();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct WaitQueueInterruptingVisitor(Exception exception) : IWaitQueueVisitor<WaitNode>
    {
        bool IWaitQueueVisitor<WaitNode>.Visit<TWaitQueue>(WaitNode node, ref TWaitQueue queue, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
        {
            queue.RemoveAndSignal(node, ref detachedQueue, exception);
            return true;
        }

        void IWaitQueueVisitor<WaitNode>.EndOfQueueReached()
        {
        }
    }

    /// <summary>
    /// Represents acquisition options.
    /// </summary>
    private protected interface IAcquisitionOptions
    {
        CancellationToken Token { get; }

        TimeSpan Timeout { get; }

        object? InterruptionReason => null;

        static virtual bool InterruptionRequired => false;
    }

    private protected interface IAcquisitionOptionsWithTimeout : IAcquisitionOptions;

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct CancellationTokenOnly : IAcquisitionOptions
    {
        internal CancellationTokenOnly(CancellationToken token) => Token = token;

        public CancellationToken Token { get; }

        TimeSpan IAcquisitionOptions.Timeout => new(Timeout.InfiniteTicks);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct TimeoutAndCancellationToken : IAcquisitionOptionsWithTimeout
    {
        internal TimeoutAndCancellationToken(TimeSpan timeout, CancellationToken token)
        {
            Timeout = timeout;
            Token = token;
        }

        public CancellationToken Token { get; }

        public TimeSpan Timeout { get; }
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

        static bool IAcquisitionOptions.InterruptionRequired => true;

        TimeSpan IAcquisitionOptions.Timeout => new(Timeout.InfiniteTicks);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct TimeoutAndInterruptionReasonAndCancellationToken : IAcquisitionOptionsWithTimeout
    {
        internal TimeoutAndInterruptionReasonAndCancellationToken(object? reason, TimeSpan timeout, CancellationToken token)
        {
            InterruptionReason = reason;
            Timeout = timeout;
            Token = token;
        }

        public CancellationToken Token { get; }

        public object? InterruptionReason { get; }

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

        protected override void CleanUp()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TContext>())
                Context = default;

            base.CleanUp();
        }

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed { get; set; }
    }

    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;
    private WaitQueueVisitor visitor;

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

        visitor = new(this);
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

    private void OnCompleted(WaitNode node) => ReturnNode(ref pool, node, ref visitor);

    private protected sealed override bool IsReadyToDispose => IsEmptyQueue;

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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = DrainWaitQueue<WaitNode, WaitQueueVisitor>(ref visitor);

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
    }

    /// <summary>
    /// Implements release semantics: attempts to resume the suspended callers.
    /// </summary>
    /// <remarks>
    /// This method invokes <se cref="ReleaseCore(TContext)"/> to modify the internal state
    /// before resuming all suspended callers.
    /// </remarks>
    /// <param name="context">The argument to be passed to <see cref="ReleaseCore(TContext)"/>.</param>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    protected void Release(TContext context)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            ReleaseCore(context);
            suspendedCallers = DrainWaitQueue<WaitNode, WaitQueueVisitor>(ref visitor);

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var manager = visitor.CreateLockManager(context);
        lock (SyncRoot)
        {
            return TryAcquire(ref manager);
        }
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
        var manager = visitor.CreateLockManager(context);
        return TryAcquireSpecialAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
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
        var manager = visitor.CreateLockManager(context);
        return AcquireSpecialAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
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
    {
        var manager = visitor.CreateLockManager(context);
        return AcquireSpecialAsync(ref pool, ref manager, new CancellationTokenOnly(token));
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct WaitQueueVisitor(QueuedSynchronizer<TContext> context) : IWaitQueueVisitor<WaitNode>
    {
        private readonly Predicate<TContext> checker = context.CanAcquire;
        private readonly Action<TContext> acquisition = context.AcquireCore;

        public LockManager CreateLockManager(TContext context) => new(context, checker, acquisition);

        bool IWaitQueueVisitor<WaitNode>.Visit<TWaitQueue>(WaitNode node,
            ref TWaitQueue queue,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
        {
            if (!checker(node.Context!))
                return false;

            if (queue.RemoveAndSignal(node, ref detachedQueue))
                acquisition(node.Context!);

            return true;
        }

        void IWaitQueueVisitor<WaitNode>.EndOfQueueReached()
        {
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct LockManager(TContext context, Predicate<TContext> checker, Action<TContext> acquisition) : ILockManager, IConsumer<WaitNode>
    {
        bool ILockManager.IsLockAllowed => checker.Invoke(context);

        void ILockManager.AcquireLock() => acquisition.Invoke(context);

        void IConsumer<WaitNode>.Invoke(WaitNode node) => node.Context = context;
    }
}