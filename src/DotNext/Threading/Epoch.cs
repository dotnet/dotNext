using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.ExceptionServices;

/// <summary>
/// Implements epoch-based reclamation.
/// </summary>
/// <seealso href="https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-579.pdf">Practical lock-freedom</seealso>
public sealed partial class Epoch
{
    private const int EpochCount = 3;
    private uint globalEpoch;
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private EpochEntryCollection entries;

    /// <summary>
    /// Initializes a new EBR implementation.
    /// </summary>
    public Epoch()
    {
        entries[0] = new(2, 1);
        entries[1] = new(0, 2);
        entries[2] = new(1, 0);
    }

    /// <summary>
    /// Enters the current epoch without the execution of the deferred actions.
    /// </summary>
    /// <remarks>
    /// This method is reentrant. It is recommended to call this method by the reader.
    /// </remarks>
    /// <returns>A scope that represents the current epoch.</returns>
    /// <exception cref="AggregateException">One or more deferred actions thrown an exception.</exception>
    public Scope Enter() => new(this);

    /// <summary>
    /// Clears all the discarded resources safely.
    /// </summary>
    /// <remarks>
    /// If another thread is in its own epoch, the method suspends.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    public async ValueTask ClearAsync(CancellationToken token = default)
    {
        var exceptions = new ExceptionAggregator();
        for (var progress = 0; !TryDrain(ref progress, ref exceptions); token.ThrowIfCancellationRequested())
        {
            await Task.Yield();
        }

        exceptions.ThrowIfNeeded();
    }

    /// <summary>
    /// Clears all the discarded resources safely.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if there is no competing thread entered the epoch, so all epochs are cleaned;
    /// otherwise, <see langword="false"/> and the caller needs to retry.
    /// </returns>
    public bool TryClear()
    {
        var exceptions = new ExceptionAggregator();
        var progress = 0;
        var result = TryDrain(ref progress, ref exceptions);
        exceptions.ThrowIfNeeded();
        return result;
    }

    /// <summary>
    /// Encapsulates actions deferred previously by <see cref="Scope.Defer(System.Action)"/> method and its overloads.
    /// </summary>
    /// <remarks>
    /// The action can be called at any point in time. The call does not necessarily have to be protected by
    /// the epoch scope.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct RecycleBin
    {
        private readonly Discardable? root;

        internal RecycleBin(Discardable? root)
            => this.root = root;

        /// <summary>
        /// Gets a value indicating that the action is empty.
        /// </summary>
        public bool IsEmpty => root is null;

        /// <summary>
        /// Invokes all deferred actions.
        /// </summary>
        /// <param name="throwOnFirstException"><see langword="true" /> if exceptions should immediately propagate; otherwise, <see langword="false" />.</param>
        public void Clear(bool throwOnFirstException = false)
            => root?.Drain(throwOnFirstException);

        /// <summary>
        /// Queues invocation of deferred actions to the thread pool.
        /// </summary>
        public void QueueCleanup()
        {
            if (root is not null)
            {
                ThreadPool.UnsafeQueueUserWorkItem(root, preferLocal: false);
            }
        }

        /// <summary>
        /// Invokes deferred actions as parallel task.
        /// </summary>
        /// <returns>The task representing execution of deferred actions.</returns>
        public Task ClearAsync()
        {
            return root is not null
                ? Task.Factory.StartNew(Execute, root, CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default)
                : Task.CompletedTask;

            static void Execute(object? callback)
            {
                Debug.Assert(callback is IThreadPoolWorkItem);

                Unsafe.As<IThreadPoolWorkItem>(callback).Execute();
            }
        }
    }

    /// <summary>
    /// Represents a scope of the region of code protected by the epoch.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"{{{nameof(DebugView)}}}")]
    public readonly struct Scope : IDisposable
    {
        private readonly Epoch state;
        private readonly uint handle;

        internal Scope(Epoch state)
        {
            this.state = state;
            handle = state.EnterEpoch();
        }

        /// <summary>
        /// Registers user action to be called at some point in future when the resource protected by the epoch is no longer
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

            state.Defer(handle, new ActionNode(callback));
        }

        /// <summary>
        /// Registers user action to be called at some point in future when the resource protected by the epoch is no longer
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

            state.Defer(handle, new ActionNode<T>(arg, callback));
        }

        /// <summary>
        /// Registers user action to be called at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="workItem">The callback to enqueue.</param>
        /// <typeparam name="TWorkItem">The type of the callback.</typeparam>
        public void Defer<TWorkItem>(TWorkItem workItem)
            where TWorkItem : struct, IThreadPoolWorkItem
            => state.Defer(handle, new WorkItem<TWorkItem>(workItem));

        /// <summary>
        /// Registers an object to be disposed at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="disposable">An object to be disposed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
        public void RegisterForDispose(IDisposable disposable)
        {
            ArgumentNullException.ThrowIfNull(disposable);

            state.Defer(handle, new Cleanup(disposable));
        }

        /// <summary>
        /// Registers an object to be disposed at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="discardable">An object to be disposed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="discardable"/> is <see langword="null"/>.</exception>
        public void RegisterForDiscard(Discardable discardable)
        {
            ArgumentNullException.ThrowIfNull(discardable);

            state.Defer(handle, discardable);
        }

        /// <summary>
        /// Reclaims the deferred actions.
        /// </summary>
        /// <param name="drainGlobalCache">
        /// <see langword="true"/> to capture all deferred actions across all threads;
        /// <see langword="false"/> to capture actions that were deferred by the current thread at some point in the past.
        /// </param>
        /// <returns>A collection of reclaimed actions that can be executed at any point in time.</returns>
        public RecycleBin Reclaim(bool drainGlobalCache = false)
            => new(state.Reclaim(handle, drainGlobalCache));

        /// <summary>
        /// Releases epoch scope.
        /// </summary>
        /// <remarks>
        /// This method is not idempotent and should not be called twice.
        /// </remarks>
        public void Dispose() => state?.ExitEpoch(handle);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugView => state is not null ? state.GetDebugView(handle) : "Empty";
    }
}