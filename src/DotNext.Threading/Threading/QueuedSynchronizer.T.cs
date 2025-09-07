using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Provides low-level infrastructure for writing custom synchronization primitives.
/// </summary>
/// <typeparam name="TContext">The context to be associated with each suspended caller.</typeparam>
public abstract class QueuedSynchronizer<TContext> : QueuedSynchronizer
{
    private new sealed class WaitNode :
        QueuedSynchronizer.WaitNode,
        INodeMapper<WaitNode, TContext>
    {
        internal TContext? Context;

        protected override void CleanUp()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TContext>())
                Context = default;

            base.CleanUp();
        }

        static TContext INodeMapper<WaitNode, TContext>.GetValue(WaitNode node)
            => node.Context!;
    }

    /// <summary>
    /// Initializes a new synchronization primitive.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is not <see langword="null"/> and less than 1.</exception>
    protected QueuedSynchronizer(int? concurrencyLevel)
        : base(concurrencyLevel)
    {
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

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
    {
        for (; !waitQueueVisitor.IsEndOfQueue<WaitNode, TContext>(out var context); waitQueueVisitor.Advance())
        {
            if (!CanAcquire(context))
                break;

            if (waitQueueVisitor.Signal())
                AcquireCore(context);
        }
    }

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
            suspendedCallers = DrainWaitQueue();

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
            suspendedCallers = DrainWaitQueue();

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

        var manager = new LockManager(this, context);
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
        var manager = new LockManager(this, context);
        return TryAcquireAsync<WaitNode, LockManager, TimeoutAndCancellationToken>(ref manager, new(timeout, token));
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
        var manager = new LockManager(this, context);
        return AcquireAsync<WaitNode, LockManager, TimeoutAndCancellationToken>(ref manager, new(timeout, token));
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
        var manager = new LockManager(this, context);
        return AcquireAsync<WaitNode, LockManager, CancellationTokenOnly>(ref manager, new(token));
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct LockManager(QueuedSynchronizer<TContext> owner, TContext context) : ILockManager, IConsumer<WaitNode>
    {
        bool ILockManager.IsLockAllowed => owner.CanAcquire(context);

        void ILockManager.AcquireLock() => owner.AcquireCore(context);

        void IConsumer<WaitNode>.Invoke(WaitNode node)
        {
            node.Context = context;
            node.DrainOnReturn = true;
        }
    }
}