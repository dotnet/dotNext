using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.CompilerServices;

/// <summary>
/// Implements epoch-based reclamation.
/// </summary>
public partial class Epoch
{
    private State epochs;
    private uint globalEpoch;

    /// <summary>
    /// Initializes a new instance of EBR manager.
    /// </summary>
    public Epoch()
    {
        epochs[0] = new(2, 1);
        epochs[1] = new(0, 2);
        epochs[2] = new(1, 0);
    }

    /// <summary>
    /// Enters the current epoch, but doesn't execute any deferred actions.
    /// </summary>
    /// <remarks>
    /// This method is reentrant.
    /// </remarks>
    /// <param name="action">An action that can be called at any point in time to invoke deferred actions.</param>
    /// <returns>A scope that represents the current epoch.</returns>
    public Guard Enter(out ReclamationAction action)
    {
        var result = new Guard(this, out var currentEpoch);
        result.AssertCounters();
        action = new(TryCollect(in result.epochEntry, currentEpoch));
        return result;
    }

    /// <summary>
    /// Enters the current epoch and invokes reclamation actions if needed.
    /// </summary>
    /// <remarks>
    /// This method is reentrant.
    /// </remarks>
    /// <param name="asyncReclamation">
    /// <see langword="true"/> to schedule reclamation actions to the thread pool;
    /// <see langword="false"/> to execute reclamation actions on this thread;
    /// <see langword="null"/> to suspend invocation of deferred actions.
    /// </param>
    /// <returns>A scope that represents the current epoch.</returns>
    public Guard Enter(bool? asyncReclamation = false)
    {
        var result = new Guard(this, out var currentEpoch);
        result.AssertCounters();
        if (asyncReclamation is null || TryCollect(in result.epochEntry, currentEpoch) is not { } callbacks)
        {
            // nothing to do
        }
        else if (asyncReclamation.GetValueOrDefault())
        {
            ThreadPool.UnsafeQueueUserWorkItem(callbacks, preferLocal: false);
        }
        else if (callbacks.InvokeAndCleanupReliably() is { IsEmpty: false } exceptions)
        {
            result.Dispose();
            exceptions.ThrowIfNeeded();
        }

        return result;
    }

    private CallbackNode? TryCollect(in Entry currentEpochState, uint currentEpochIndex)
    {
        var nextEpochIndex = currentEpochState.Next;
        ref var prevEpochState = ref epochs[currentEpochState.Previous];
        ref var nextEpochState = ref epochs[nextEpochIndex];

        return prevEpochState.Counter is 0U
               && nextEpochState.Counter is 0U
               && Interlocked.CompareExchange(ref globalEpoch, nextEpochIndex, currentEpochIndex) == currentEpochIndex
            ? prevEpochState.Detach()
            : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Defer(CallbackNode node)
    {
        epochs[globalEpoch].Push(node);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // acts as compiler-level barrier to avoid caching of 'globalEpoch' field
    private ref Entry GetCurrentEpoch(out uint currentEpoch)
        => ref epochs[currentEpoch = globalEpoch];

    /// <summary>
    /// Invokes all deferred actions across all epochs.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe and cannot be called concurrently with other threads entered a protected region
    /// with <see cref="Enter(bool?)"/>. The caller must ensure that all threads finished their work prior to this method.
    /// </remarks>
    /// <param name="asyncReclamation">
    /// <see langword="true"/> to schedule reclamation actions to the thread pool;
    /// <see langword="false"/> to execute reclamation actions on this thread.
    /// </param>
    /// <exception cref="InvalidOperationException">Not all threads relying on the current instance finished their work.</exception>
    public void UnsafeReclaim(bool asyncReclamation = false)
    {
        foreach (ref var state in epochs)
        {
            if (state.Counter > 0UL)
            {
                throw new InvalidOperationException();
            }

            if (state.Detach() is not { } callbacks)
            {
                // nothing to do
            }
            else if (asyncReclamation)
            {
                ThreadPool.UnsafeQueueUserWorkItem(callbacks, preferLocal: false);
            }
            else
            {
                callbacks.InvokeAndCleanupReliablyAndThrowIfNeeded();
            }
        }
    }

    /// <summary>
    /// Represents reclamation action.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReclamationAction : IFunctional<Action>
    {
        private readonly CallbackNode? callbacks;

        internal ReclamationAction(CallbackNode? callbacks) => this.callbacks = callbacks;

        /// <summary>
        /// Gets a value indicating that the action is empty.
        /// </summary>
        public bool IsEmpty => callbacks is null;

        /// <summary>
        /// Converts this action to delegate instance.
        /// </summary>
        /// <param name="throwOnFirstException"><see langword="true" /> if exceptions should immediately propagate; otherwise, <see langword="false" />.</param>
        /// <returns>A delegate that represents reclamation action.</returns>
        public Action ToDelegate(bool throwOnFirstException = false)
        {
            return callbacks is null
                ? NoOp
                : throwOnFirstException
                    ? callbacks.InvokeAndCleanup
                    : callbacks.InvokeAndCleanupReliablyAndThrowIfNeeded;

            static void NoOp()
            {
                // nothing to do
            }
        }

        /// <inheritdoc cref="IFunctional{TDelegate}.ToDelegate()"/>
        Action IFunctional<Action>.ToDelegate() => ToDelegate();

        /// <summary>
        /// Invokes all deferred actions.
        /// </summary>
        /// <param name="throwOnFirstException"><see langword="true" /> if exceptions should immediately propagate; otherwise, <see langword="false" />.</param>
        public void Invoke(bool throwOnFirstException = false)
        {
            if (callbacks is null)
            {
                // nothing to do
            }
            else if (throwOnFirstException)
            {
                callbacks.InvokeAndCleanup();
            }
            else
            {
                callbacks.InvokeAndCleanupReliablyAndThrowIfNeeded();
            }
        }

        /// <summary>
        /// Queues invocation of deferred actions to the thread pool.
        /// </summary>
        /// <param name="throwOnFirstException"><see langword="true" /> if exceptions should immediately propagate; otherwise, <see langword="false" />.</param>
        public void Start(bool throwOnFirstException = false)
        {
            if (callbacks is null)
            {
                // nothing to do
            }
            else if (throwOnFirstException)
            {
                ThreadPool.UnsafeQueueUserWorkItem(callbacks, preferLocal: false);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(static callbacks => callbacks.InvokeAndCleanup(), callbacks, preferLocal: false);
            }
        }
    }

    /// <summary>
    /// Represents a scope of the region of code protected by the epoch.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"{{{nameof(DebugView)}}}")]
    public readonly ref struct Guard
    {
        private readonly Epoch epoch;
        internal readonly ref Entry epochEntry;

        internal Guard(Epoch instance, out uint currentEpoch)
        {
            epoch = instance;
            epochEntry = ref instance.GetCurrentEpoch(out currentEpoch);

            Interlocked.Increment(ref epochEntry.Counter);
        }

        [Conditional("DEBUG")]
        internal void AssertCounters()
        {
            Debug.Assert(epochEntry.Counter > 0U);

            var prevEpochIndex = epochEntry.Previous;
            var prevEpochThreads = epoch.epochs[prevEpochIndex].Counter;

            var nextEpochIndex = epochEntry.Next;
            var nextEpochThreads = epoch.epochs[nextEpochIndex].Counter;

            Debug.Assert(prevEpochThreads is 0U || nextEpochThreads is 0U, $"Epoch #{prevEpochIndex}={prevEpochThreads}, Epoch#{nextEpochIndex}={nextEpochThreads}");
        }

        /// <summary>
        /// Queues user action to be called at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <remarks>
        /// Invocation order of callbacks is not guaranteed.
        /// </remarks>
        /// <param name="callback">The callback to enqueue.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        public void Defer(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            epoch.Defer(new ActionNode(callback));
        }

        /// <summary>
        /// Queues user action to be called at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <remarks>
        /// Invocation order of callbacks is not guaranteed.
        /// </remarks>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <param name="callback">The callback to enqueue.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        public void Defer<T>(T arg, Action<T> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            epoch.Defer(new ActionNode<T>(arg, callback));
        }

        /// <summary>
        /// Queues user action to be called at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="workItem">The callback to enqueue.</param>
        /// <typeparam name="TWorkItem">The type of the callback.</typeparam>
        public void Defer<TWorkItem>(TWorkItem workItem)
            where TWorkItem : struct, IThreadPoolWorkItem
            => epoch.Defer(new WorkItemNode<TWorkItem>(workItem));

        /// <summary>
        /// Queues an object to be disposed at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="disposable">An object to be disposed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
        public void Defer(IDisposable disposable)
        {
            ArgumentNullException.ThrowIfNull(disposable);

            epoch.Defer(new CleanupNode(disposable));
        }

        /// <summary>
        /// Releases epoch scope.
        /// </summary>
        /// <remarks>
        /// This method is not idempotent and should not be called twice.
        /// </remarks>
        public void Dispose() => Interlocked.Decrement(ref epochEntry.Counter);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugView => epoch is null ? "Empty" : epochEntry.DebugView;
    }
}