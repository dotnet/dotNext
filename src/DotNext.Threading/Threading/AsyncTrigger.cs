using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime;
using Runtime.CompilerServices;
using Tasks;

/// <summary>
/// Represents asynchronous trigger that allows to resume and suspend
/// concurrent flows.
/// </summary>
public class AsyncTrigger : QueuedSynchronizer, IAsyncEvent
{
    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    public AsyncTrigger()
    {
    }

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
        => Debug.Fail("Should not be called");

    private bool SignalCore<TVisitor>(TVisitor visitor)
        where TVisitor : struct, IWaitQueueVisitor
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        bool signaled;
        using (AcquireInternalLock())
        {
            signaled = DrainWaitQueue(visitor, out suspendedCallers);
        }

        suspendedCallers?.Unwind();
        return signaled;
    }

    private bool DrainWaitQueue<TVisitor>(scoped TVisitor visitor, out LinkedValueTaskCompletionSource<bool>? suspendedCallers)
        where TVisitor : struct, IWaitQueueVisitor, allows ref struct
    {
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        var waitQueue = GetWaitQueue(ref detachedQueue);
        var signaled = visitor.Visit(ref waitQueue);
        suspendedCallers = detachedQueue.First;
        return signaled;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Reset() => false;

    /// <summary>
    /// Resumes the first suspended caller in the wait queue.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume all suspended callers in the queue;
    /// <see langword="false"/> to resume the first suspended caller in the queue.
    /// </param>
    /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public bool Signal(bool resumeAll = false)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return SignalCore(new ResumingVisitor(resumeAll));
    }

    /// <summary>
    /// Interrupts all the suspended callers.
    /// </summary>
    /// <param name="reason">The interruption reason.</param>
    /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public bool Interrupt(object? reason)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return SignalCore(new InterruptingVisitor(reason));
    }

    /// <inheritdoc/>
    bool IAsyncEvent.IsSet => false;

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Signal();

    private T WaitAsync<T, TBuilder>(ref TBuilder builder)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, WaitNode>(ref builder, acquired: false) is { } node:
                node.DrainOnReturn = false;
                goto default;
            default:
                builder.Dispose();
                break;
        }

        return builder.Invoke();
    }

    /// <summary>
    /// Suspends the caller and waits for the signal.
    /// </summary>
    /// <remarks>
    /// This method always suspends the caller.
    /// </remarks>
    /// <param name="timeout">The time to wait for the signal.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="Signal(bool)"/>
    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = CreateTaskBuilder(timeout, token);
        return WaitAsync<ValueTask<bool>, TimeoutAndCancellationToken>(ref builder);
    }

    /// <summary>
    /// Suspends the caller and waits for the signal.
    /// </summary>
    /// <remarks>
    /// This method always suspends the caller.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="Signal(bool)"/>
    public ValueTask WaitAsync(CancellationToken token = default)
    {
        var builder = CreateTaskBuilder(token);
        return WaitAsync<ValueTask, CancellationTokenOnly>(ref builder);
    }

    /// <summary>
    /// Resumes the first suspended caller in the queue and suspends the immediate caller.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume all suspended callers in the queue;
    /// <see langword="false"/> to resume the first suspended caller in the queue.
    /// </param>
    /// <param name="throwOnEmptyQueue">
    /// <see langword="true"/> to throw <see cref="InvalidOperationException"/> if there is no suspended callers to resume;
    /// <see langword="false"/> to suspend the caller.
    /// </param>
    /// <param name="timeout">The time to wait for the signal.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
    public ValueTask<bool> SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
    {
        var builder = CreateTaskBuilder(timeout, token);
        return SignalAndWaitAsync<ValueTask<bool>, TimeoutAndCancellationToken, ResumingVisitor>(
            ref builder,
            new(resumeAll),
            throwOnEmptyQueue
        );
    }

    /// <summary>
    /// Resumes the first suspended caller in the queue and suspends the immediate caller.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume all suspended callers in the queue;
    /// <see langword="false"/> to resume the first suspended caller in the queue.
    /// </param>
    /// <param name="throwOnEmptyQueue">
    /// <see langword="true"/> to throw <see cref="InvalidOperationException"/> if there is no suspended callers to resume;
    /// <see langword="false"/> to suspend the caller.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
    public ValueTask SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, CancellationToken token = default)
    {
        var builder = CreateTaskBuilder(token);
        return SignalAndWaitAsync<ValueTask, CancellationTokenOnly, ResumingVisitor>(
            ref builder,
            new(resumeAll),
            throwOnEmptyQueue);
    }

    private T SignalAndWaitAsync<T, TBuilder, TVisitor>(ref TBuilder builder, TVisitor visitor, bool throwOnEmptyQueue)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
        where TVisitor : struct, IWaitQueueVisitor<T>, allows ref struct
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        if (builder.IsCompleted)
        {
            suspendedCallers = null;
        }
        else if (DrainWaitQueue(visitor, out suspendedCallers))
        {
            Acquire<T, TBuilder, WaitNode>(ref builder, acquired: false);
        }
        else if (throwOnEmptyQueue)
        {
            builder.Dispose();
            return TVisitor.EmptyQueueTask;
        }

        builder.Dispose();
        suspendedCallers?.Unwind();
        return builder.Invoke();
    }

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="TCondition">The type of predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="timeout">The number of time to wait before this event is set.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public ValueTask<bool> SpinWaitAsync<TCondition>(TCondition condition, TimeSpan timeout, CancellationToken token = default)
        where TCondition : ISupplier<bool>
        => SpinWaitAsync(condition, new Timeout(timeout), token);

    private async ValueTask<bool> SpinWaitAsync<TCondition>(TCondition condition, Timeout timeout, CancellationToken token)
        where TCondition : ISupplier<bool>
    {
        do
        {
            if (condition.Invoke())
                return true;
        } while (timeout.TryGetRemainingTime(out var remainingTime) &&
                 await TryAcquireAsync<WaitNode, ConditionalLockManager<TCondition>>(new(ref condition), remainingTime, token).ConfigureAwait(false));

        return false;
    }

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="TCondition">The type of predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask SpinWaitAsync<TCondition>(TCondition condition, CancellationToken token = default)
        where TCondition : ISupplier<bool>
    {
        while (!condition.Invoke())
            await AcquireAsync<WaitNode, ConditionalLockManager<TCondition>>(new(ref condition), token)
                .ConfigureAwait(false);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ConditionalLockManager<TCondition>(ref TCondition condition) : ILockManager, IConsumer<WaitNode>
        where TCondition : ISupplier<bool>
    {
        private readonly ref TCondition condition = ref condition;
        
        bool ILockManager.IsLockAllowed => condition.Invoke();

        void ILockManager.AcquireLock()
        {
        }

        void IConsumer<WaitNode>.Invoke(WaitNode node) => node.DrainOnReturn = false;

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }
    
    private interface IWaitQueueVisitor
    {
        bool Visit(scoped ref WaitQueueVisitor waitQueueVisitor);
    }
    
    private interface IWaitQueueVisitor<out T> : IWaitQueueVisitor
        where T : struct, IEquatable<T>
    {
        static abstract T EmptyQueueTask { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ResumingVisitor(bool resumeAll) : IWaitQueueVisitor<ValueTask>, IWaitQueueVisitor<ValueTask<bool>>
    {
        bool IWaitQueueVisitor.Visit(scoped ref WaitQueueVisitor waitQueueVisitor)
        {
            bool signaled;
            if (resumeAll)
            {
                waitQueueVisitor.SignalAll(out signaled);
            }
            else
            {
                waitQueueVisitor.SignalFirst(out signaled);
            }

            return signaled;
        }

        static ValueTask IWaitQueueVisitor<ValueTask>.EmptyQueueTask
            => ValueTask.FromException(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));

        static ValueTask<bool> IWaitQueueVisitor<ValueTask<bool>>.EmptyQueueTask
            => ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct InterruptingVisitor : IWaitQueueVisitor
    {
        private readonly PendingTaskInterruptedException exception;

        public InterruptingVisitor(object? reason)
            => ExceptionDispatchInfo.SetCurrentStackTrace(exception = new() { Reason = reason });

        bool IWaitQueueVisitor.Visit(scoped ref WaitQueueVisitor waitQueueVisitor)
        {
            waitQueueVisitor.SignalAll(exception, out bool signaled);
            return signaled;
        }
    }
}