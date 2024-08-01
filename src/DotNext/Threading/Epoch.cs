using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime;
using Runtime.ExceptionServices;

/// <summary>
/// Implements epoch-based reclamation.
/// </summary>
/// <seealso href="https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-579.pdf">Practical lock-freedom</seealso>
public partial class Epoch
{
    private State state = new();

    /// <summary>
    /// Enters the current epoch, but doesn't execute any deferred actions.
    /// </summary>
    /// <remarks>
    /// This method is reentrant and never throws an exception.
    /// </remarks>
    /// <param name="drainGlobalCache">
    /// <see langword="true"/> to capture all deferred actions across all threads;
    /// <see langword="false"/> to capture actions that were deferred by the current thread at some point in the past.
    /// </param>
    /// <param name="bin">An action that can be called at any point in time to invoke deferred actions.</param>
    /// <returns>A scope that represents the current epoch.</returns>
    public Scope Enter(bool drainGlobalCache, out RecycleBin bin)
    {
        var scope = new Scope(ref state);
        state.AssertCounters(scope.Handle);

        Reclaim(scope.Handle, drainGlobalCache, out bin);
        return scope;
    }

    /// <summary>
    /// Enters the current epoch, and optionally executes a deferred actions.
    /// </summary>
    /// <remarks>
    /// This method is reentrant. In case of exceptions in one or more deferred actions the method closes the scope correctly.
    /// </remarks>
    /// <param name="drainGlobalCache">
    /// <see langword="null"/> to avoid reclamation;
    /// <see langword="true"/> to capture all deferred actions across all threads;
    /// <see langword="false"/> to capture actions that were deferred by the current thread at some point in the past.
    /// </param>
    /// <returns>A scope that represents the current epoch.</returns>
    /// <exception cref="AggregateException">One or more deferred actions thrown an exception.</exception>
    public Scope Enter(bool? drainGlobalCache = false)
    {
        var scope = new Scope(ref state);
        state.AssertCounters(scope.Handle);

        if (drainGlobalCache.HasValue && Reclaim(scope.Handle, drainGlobalCache.GetValueOrDefault()) is { IsEmpty: false } exceptions)
        {
            scope.Dispose();
            exceptions.ThrowIfNeeded();
        }

        return scope;
    }

    /// <summary>
    /// Enters the current epoch, and optionally executes a deferred actions.
    /// </summary>
    /// <remarks>
    /// This method is reentrant. In case of exceptions in one or more deferred actions the aggregated exception is propagated
    /// to the caller. In this case, the caller is responsible to close the scope correctly.
    /// </remarks>
    /// <param name="drainGlobalCache">
    /// <see langword="true"/> to capture all deferred actions across all threads;
    /// <see langword="false"/> to capture actions that were deferred by the current thread at some point in the past.
    /// </param>
    /// <param name="scope">The protection scope. It is initialized in case of exceptions raised by the deferred actions invoked by the method.</param>
    /// <exception cref="AggregateException">One or more deferred actions thrown an exception.</exception>
    public void Enter(bool drainGlobalCache, out Scope scope)
    {
        scope = new(ref state);
        state.AssertCounters(scope.Handle);

        UnsafeReclaim(scope.Handle, drainGlobalCache);
    }

    /// <summary>
    /// Enters the current epoch, and optionally executes a deferred actions.
    /// </summary>
    /// <remarks>
    /// This method is reentrant. In case of exceptions in one or more deferred actions the aggregated exception is propagated
    /// to the caller. There is no way for the caller to close the scope correctly.
    /// </remarks>
    /// <param name="drainGlobalCache">
    /// <see langword="true"/> to capture all deferred actions across all threads;
    /// <see langword="false"/> to capture actions that were deferred by the current thread at some point in the past.
    /// </param>
    /// <exception cref="AggregateException">One or more deferred actions thrown an exception.</exception>
    public Scope UnsafeEnter(bool drainGlobalCache = false)
    {
        Enter(drainGlobalCache, out Scope scope);
        return scope;
    }
    
    private void Reclaim(uint protectedEntryHandle, bool drainGlobalCache, out RecycleBin action)
    {
        if (state.TryBumpEpoch(protectedEntryHandle) is not { IsEmpty: false } garbage)
        {
            action = default;
        }
        else if (drainGlobalCache)
        {
            action = new(garbage.ReclaimGlobal());
        }
        else
        {
            action = new(garbage.ReclaimLocal());
        }
    }

    private ExceptionAggregator Reclaim(uint protectedEntryHandle, bool drainGlobalCache)
    {
        var exceptions = new ExceptionAggregator();
        if (state.TryBumpEpoch(protectedEntryHandle) is { IsEmpty: false } garbage)
        {
            if (drainGlobalCache)
            {
                foreach (var bucket in garbage.ReclaimGlobal())
                {
                    bucket.Drain(ref exceptions);
                }
            }
            else if (garbage.ReclaimLocal() is { } bucket)
            {
                bucket.Drain(ref exceptions);
            }
        }

        return exceptions;
    }

    private void UnsafeReclaim(uint protectedEntryHandle, bool drainGlobalCache)
    {
        if (state.TryBumpEpoch(protectedEntryHandle) is not { IsEmpty: false } garbage)
        {
            // do nothing
        }
        else if (drainGlobalCache)
        {
            foreach (var bucket in garbage.ReclaimGlobal())
            {
                bucket.Drain();
            }
        }
        else if (garbage.ReclaimLocal() is { } bucket)
        {
            bucket.Drain();
        }
    }

    /// <summary>
    /// Invokes all deferred actions across all epochs.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe and cannot be called concurrently with other threads entered a protected region
    /// with <see cref="Enter(bool?)"/>. The caller must ensure that all threads finished their work prior to this method.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Not all threads relying on the current instance finished their work.</exception>
    /// <exception cref="AggregateException">One or more deferred actions throw an exception.</exception>
    public void UnsafeClear()
    {
        var exceptions = new ExceptionAggregator();
        state.UnsafeDrain(ref exceptions);
        exceptions.ThrowIfNeeded();
    }

    /// <summary>
    /// Encapsulates actions deferred previously by <see cref="Scope.Defer(System.Action)"/> method and its overloads.
    /// </summary>
    /// <remarks>
    /// The action can be called at any point in time. The call does not necessarily have to be protected by
    /// the epoch scope.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct RecycleBin : IThreadPoolWorkItem
    {
        // null, or Discardable, or Discardable[]
        private readonly object discardable;
        private readonly int count;

        internal RecycleBin(Discardable? discardable)
        {
            if (discardable is null)
            {
                this.discardable = Sentinel.Instance;
                count = 0;
            }
            else
            {
                this.discardable = discardable;
                count = 1;
            }
        }

        internal RecycleBin(DetachingEnumerable discardables)
        {
            var array = new Discardable[discardables.MaxCount];

            var index = 0;
            foreach (var node in discardables)
            {
                // Perf: skip list.Add to avoid capacity checks
                array[index++] = node;
            }

            switch (index)
            {
                case 0:
                    discardable = Sentinel.Instance;
                    break;
                case 1:
                    discardable = array[0];
                    count = 1;
                    break;
                default:
                    count = index;
                    discardable = array;
                    break;
            }
        }

        [UnscopedRef]
        private ReadOnlySpan<Discardable> Span => count switch
        {
            0 => ReadOnlySpan<Discardable>.Empty,
            1 => new(in Intrinsics.InToRef<object, Discardable>(in discardable)),
            _ => new(Unsafe.As<Discardable[]>(discardable), 0, count),
        };

        /// <summary>
        /// Gets a value indicating that the action is empty.
        /// </summary>
        public bool IsEmpty => Span.IsEmpty;

        /// <summary>
        /// Invokes all deferred actions.
        /// </summary>
        /// <param name="throwOnFirstException"><see langword="true" /> if exceptions should immediately propagate; otherwise, <see langword="false" />.</param>
        public void Clear(bool throwOnFirstException = false)
        {
            scoped var span = Span;
            switch (span)
            {
                case []:
                    break;
                case [var callback]:
                    callback.Drain(throwOnFirstException);
                    break;
                default:
                    Drain(span, throwOnFirstException);
                    break;
            }
        }

        /// <inheritdoc cref="IThreadPoolWorkItem.Execute()"/>
        void IThreadPoolWorkItem.Execute() => Clear(throwOnFirstException: false);

        private static void Drain(ReadOnlySpan<Discardable> objects, bool throwOnFirstException)
        {
            var exceptions = new ExceptionAggregator();
            if (throwOnFirstException)
            {
                try
                {
                    foreach (var node in objects)
                    {
                        node.Drain();
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            else
            {
                foreach (var node in objects)
                {
                    node.Drain(ref exceptions);
                }
            }

            exceptions.ThrowIfNeeded();
        }

        /// <summary>
        /// Queues invocation of deferred actions to the thread pool.
        /// </summary>
        public void QueueCleanup()
        {
            scoped var span = Span;

            switch (span)
            {
                case []:
                    break;
                case [var callback]:
                    IThreadPoolWorkItem workItem = callback;
                    goto queue_user_work_item;
                default:
                    workItem = this;
                    queue_user_work_item:
                    ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
                    break;
            }
        }

        /// <summary>
        /// Invokes deferred actions as parallel task.
        /// </summary>
        /// <returns>The task representing execution of deferred actions.</returns>
        public Task ClearAsync()
        {
            scoped var span = Span;
            Task task;

            switch (span)
            {
                case []:
                    task = Task.CompletedTask;
                    break;
                case [var callback]:
                    IThreadPoolWorkItem workItem = callback;
                    goto start_task;
                default:
                    workItem = this;
                    start_task:
                    task = Task.Factory.StartNew(Execute, workItem, CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                    break;
            }

            return task;

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
    public readonly ref struct Scope
    {
        internal readonly uint Handle;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ref State state;

        internal Scope(ref State state)
        {
            this.state = ref state;
            Handle = state.Enter();
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

            state.Defer(new ActionNode(callback));
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

            state.Defer(new ActionNode<T>(arg, callback));
        }

        /// <summary>
        /// Registers user action to be called at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="workItem">The callback to enqueue.</param>
        /// <typeparam name="TWorkItem">The type of the callback.</typeparam>
        public void Defer<TWorkItem>(TWorkItem workItem)
            where TWorkItem : struct, IThreadPoolWorkItem
            => state.Defer(new WorkItem<TWorkItem>(workItem));

        /// <summary>
        /// Registers an object to be disposed at some point in future when the resource protected by the epoch is no longer
        /// available to any consumer.
        /// </summary>
        /// <param name="disposable">An object to be disposed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
        public void RegisterForDispose(IDisposable disposable)
        {
            ArgumentNullException.ThrowIfNull(disposable);

            state.Defer(new Cleanup(disposable));
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

            state.Defer(discardable);
        }

        /// <summary>
        /// Releases epoch scope.
        /// </summary>
        /// <remarks>
        /// This method is not idempotent and should not be called twice.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Dispose()
        {
            ref var stateCopy = ref state;
            if (Unsafe.IsNullRef(in stateCopy) is false)
                state.Exit(Handle);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugView
        {
            get
            {
                ref var stateCopy = ref state;
                return Unsafe.IsNullRef(in stateCopy) ? "Empty" : stateCopy.GetDebugView(Handle);
            }
        }
    }
}