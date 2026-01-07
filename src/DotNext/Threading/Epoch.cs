using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.CompilerServices;
using Runtime.ExceptionServices;

/// <summary>
/// Implements epoch-based reclamation.
/// </summary>
/// <seealso href="https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-579.pdf">Practical lock-freedom</seealso>
public partial class Epoch
{
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
    /// Invokes all deferred actions across all epochs.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe and cannot be called concurrently with other threads entered a protected region
    /// with <see cref="Enter()"/>. The caller must ensure that all threads finished their work prior to this method.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Not all threads relying on the current instance finished their work.</exception>
    /// <exception cref="AggregateException">One or more deferred actions throw an exception.</exception>
    public void UnsafeClear()
    {
        var exceptions = new ExceptionAggregator();
        UnsafeDrain(ref exceptions);
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
            1 => new(in Unsafe.InToRef<object, Discardable>(in discardable)),
            _ => new(Unsafe.As<Discardable[]>(discardable), 0, count),
        };

        /// <summary>
        /// Gets a value indicating that the action is empty.
        /// </summary>
        public bool IsEmpty => count is 0;

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
                    exceptions += e;
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
            => state.Reclaim(handle, drainGlobalCache);

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