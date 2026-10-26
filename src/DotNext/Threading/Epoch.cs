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
    /// Gets or sets the manager of the unmanaged resources protected by this epoch.
    /// </summary>
    /// <remarks>
    /// The manager is required to defer the release of the unmanaged resources. <see cref="Epoch"/>
    /// uses it to link the deferred resources together, and to release them when they are no longer
    /// available to any consumer.
    /// </remarks>
    [CLSCompliant(false)]
    public INativeResourceManager? NativeResourceManager
    {
        get;
        init;
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
    /// Encapsulates actions deferred previously by <see cref="Scope.Defer(System.Action)"/> method and its overloads,
    /// and the unmanaged resources to be released by <see cref="NativeResourceManager"/>.
    /// </summary>
    /// <remarks>
    /// The action can be called at any point in time. The call does not necessarily have to be protected by
    /// the epoch scope. The bin must be cleared only once: clearing the copies of the same bin invokes the actions
    /// and releases the unmanaged resources more than once.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct RecycleBin
    {
        private readonly Discardable? root;
        private readonly INativeResourceManager? resourceManager;
        private readonly nuint rootHandle;

        internal RecycleBin(Discardable? root, nuint rootHandle, INativeResourceManager? resourceManager)
        {
            Debug.Assert(resourceManager is not null || rootHandle is 0U);
            
            this.root = root;
            this.resourceManager = resourceManager;
            this.rootHandle = rootHandle;
        }

        /// <summary>
        /// Gets a value indicating that the action is empty.
        /// </summary>
        public bool IsEmpty => root is null && rootHandle is 0U;

        /// <summary>
        /// Invokes all deferred actions.
        /// </summary>
        /// <param name="throwOnFirstException"><see langword="true" /> if exceptions should immediately propagate; otherwise, <see langword="false" />.</param>
        public void Clear(bool throwOnFirstException = false)
        {
            if (throwOnFirstException)
            {
                ClearCore();
            }
            else
            {
                var exceptions = new ExceptionAggregator();
                ClearCore(ref exceptions);
                exceptions.ThrowIfNeeded();
            }
        }

        private void ClearCore(ref ExceptionAggregator exceptions)
        {
            root?.Drain(ref exceptions);
            if (rootHandle is not 0U)
            {
                Debug.Assert(resourceManager is not null);

                INativeResourceManager.Clear(resourceManager, rootHandle, ref exceptions);
            }
        }

        private void ClearCore()
        {
            // a failed managed action must not prevent the release of unmanaged resources
            if (rootHandle is not 0U)
            {
                Debug.Assert(resourceManager is not null);

                INativeResourceManager.Clear(resourceManager, rootHandle);
            }

            root?.Drain();
        }

        /// <summary>
        /// Queues invocation of deferred actions to the thread pool.
        /// </summary>
        public void QueueCleanup()
        {
            switch (root is not null, rootHandle is not 0U)
            {
                case (false, false):
                    break;
                case (true, false):
                    Debug.Assert(root is not null);
                    ThreadPool.UnsafeQueueUserWorkItem(root, preferLocal: false);
                    break;
                case (false, true):
                    Debug.Assert(resourceManager is not null);
                    unsafe
                    {
                        ThreadPool.UnsafeQueueUserWorkItem(
                            Action<nuint>.FromPointer(&ClearNativeOnly, resourceManager),
                            rootHandle,
                            preferLocal: false);
                    }

                    break;
                case (true, true):
                    Debug.Assert(root is not null);
                    Debug.Assert(resourceManager is not null);
                    ThreadPool.UnsafeQueueUserWorkItem(
                        static args => ClearAll(args.root, args.resourceManager, args.rootHandle),
                        (root, resourceManager, rootHandle),
                        preferLocal: false);
                    break;
            }
        }
        
        private static void ClearNativeOnly(INativeResourceManager manager, nuint handle)
        {
            var exceptions = new ExceptionAggregator();
            INativeResourceManager.Clear(manager, handle, ref exceptions);
            exceptions.ThrowIfNeeded();
        }

        private static void ClearAll(Discardable root, INativeResourceManager manager, nuint rootHandle)
        {
            var exceptions = new ExceptionAggregator();
            root.Drain(ref exceptions);
            INativeResourceManager.Clear(manager, rootHandle, ref exceptions);
            exceptions.ThrowIfNeeded();
        }

        /// <summary>
        /// Invokes deferred actions as parallel task.
        /// </summary>
        /// <returns>The task representing execution of deferred actions.</returns>
        public Task ClearAsync()
        {
            Action<object?> action;
            object? state;
            switch (root is not null, rootHandle is not 0U)
            {
                case (false, false):
                    return Task.CompletedTask;
                case (true, false):
                    action = ClearManagedOnly;
                    state = root;
                    break;
                case (false, true):
                    Debug.Assert(resourceManager is not null);
                    state = new Tuple<INativeResourceManager, nuint>(resourceManager, rootHandle);
                    action = ClearNativeOnly;
                    break;
                case (true, true):
                    Debug.Assert(root is not null);
                    Debug.Assert(resourceManager is not null);
                    state = new Tuple<Discardable, INativeResourceManager, nuint>(root, resourceManager, rootHandle);
                    action = ClearAll;
                    break;
            }

            return Task.Factory.StartNew(action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            static void ClearManagedOnly(object? state)
            {
                Debug.Assert(state is Discardable);

                Unsafe.As<IThreadPoolWorkItem>(state).Execute();
            }

            static void ClearNativeOnly(object? state)
            {
                Debug.Assert(state is Tuple<INativeResourceManager, nuint>);

                var args = Unsafe.As<Tuple<INativeResourceManager, nuint>>(state);
                RecycleBin.ClearNativeOnly(args.Item1, args.Item2);
            }

            static void ClearAll(object? state)
            {
                Debug.Assert(state is Tuple<Discardable, INativeResourceManager, nuint>);
                
                var args = Unsafe.As<Tuple<Discardable, INativeResourceManager, nuint>>(state);
                RecycleBin.ClearAll(args.Item1, args.Item2, args.Item3);
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
        /// Registers the unmanaged resource to be released at some point in future when it is no longer
        /// available to any consumer.
        /// </summary>
        /// <remarks>
        /// The resource is released by <see cref="NativeResourceManager"/>. The location returned by
        /// <see cref="INativeResourceManager.GetNextHandle(nuint)"/> for the resource is owned by the epoch until the release.
        /// </remarks>
        /// <param name="resourceHandle">The handle of the unmanaged resource.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="resourceHandle"/> is zero.</exception>
        /// <exception cref="InvalidOperationException"><see cref="NativeResourceManager"/> is not set.</exception>
        [CLSCompliant(false)]
        public void Defer(nuint resourceHandle)
        {
            ArgumentOutOfRangeException.ThrowIfZero(resourceHandle);

            if (state.NativeResourceManager is null)
                InvalidOperationException.Throw();
            
            state.Defer(handle, resourceHandle);
        }

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
            => new(state.Reclaim(handle, drainGlobalCache, out var unmanaged), unmanaged, state.NativeResourceManager);

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
    
    /// <summary>
    /// Represents a manager of the unmanaged resources which release is deferred by <see cref="Epoch"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Epoch"/> links the deferred resources together without allocations. The link is stored
    /// in the resource itself, at the location provided by <see cref="GetNextHandle(nuint)"/>.
    /// </remarks>
    /// <seealso cref="NativeResourceManager"/>
    [CLSCompliant(false)]
    public partial interface INativeResourceManager
    {
        /// <summary>
        /// Gets a reference to the location inside the resource reserved for the link to the next deferred resource.
        /// </summary>
        /// <remarks>
        /// <see cref="Epoch"/> owns the location from the moment the resource is deferred until it is released.
        /// During this period, the location must not be read or modified by any other code, including the consumers
        /// that still access the resource. The location must be the same for every call with the same handle.
        /// </remarks>
        /// <param name="handle">The handle of the resource.</param>
        /// <returns>A reference to the location of the link.</returns>
        ref nuint GetNextHandle(nuint handle);

        /// <summary>
        /// Releases the resource.
        /// </summary>
        /// <remarks>
        /// <see cref="Epoch"/> calls this method when the resource is no longer available to any consumer.
        /// The method can be called on any thread, and outside the epoch scope.
        /// </remarks>
        /// <param name="handle">The handle of the resource.</param>
        void Release(nuint handle);
    }
}