using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents a pipe to process asynchronous tasks as they complete.
/// </summary>
/// <typeparam name="T">The type of the task.</typeparam>
public partial class TaskCompletionPipe<T> : IAsyncEnumerable<T>, IResettable
    where T : Task
{
    // Represents a number of scheduled tasks which can be greater than the number of enqueued tasks
    // because only completed task can be enqueued
    private uint scheduledTasksCount;
    private bool completionRequested;

    // Allows to skip scheduled tasks in case of reuse
    private uint version;

    /// <summary>
    /// Initializes a new pipe.
    /// </summary>
    public TaskCompletionPipe() => pool = new(OnCompleted);

    private object SyncRoot => this;

    private void OnCompleted(Signal signal)
    {
        lock (SyncRoot)
        {
            if (signal.NeedsRemoval)
                RemoveNode(signal);

            pool.Return(signal);
        }
    }

    private LinkedValueTaskCompletionSource<bool>? CompleteCore()
    {
        lock (SyncRoot)
        {
            if (completionRequested)
                throw new InvalidOperationException();

            completionRequested = true;
            return scheduledTasksCount is 0U ? DetachWaitQueue() : null;
        }
    }

    /// <summary>
    /// Marks the pipe as being complete, meaning no more items will be added to it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The pipe is already completed.</exception>
    public void Complete() => CompleteCore()?.TrySetResultAndSentinelToAll(result: false);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool IsCompleted
    {
        get
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            return scheduledTasksCount is 0U && completionRequested;
        }
    }

    private bool TryAdd(T task, out uint currentVersion, out LinkedValueTaskCompletionSource<bool>? waitNode)
    {
        bool result;

        lock (SyncRoot)
        {
            if (completionRequested)
                throw new InvalidOperationException();

            scheduledTasksCount++;
            currentVersion = version;

            waitNode = (result = task.IsCompleted)
                ? EnqueueCompletedTask(new(task))
                : null;
        }

        return result;
    }

    /// <summary>
    /// Adds the task to this pipe.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The pipe is closed.</exception>
    public void Add(T task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!TryAdd(task, out var expectedVersion, out var waitNode))
        {
            Debug.Assert(waitNode is null);
            task.ConfigureAwait(false).GetAwaiter().OnCompleted(new LazyLinkedTaskNode(task, this, expectedVersion).Invoke);
        }
        else if (waitNode is not null)
        {
            waitNode.TrySetResultAndSentinelToAll(result: true);
        }
    }

    private LinkedValueTaskCompletionSource<bool>? ResetCore()
    {
        lock (SyncRoot)
        {
            version += 1U;
            scheduledTasksCount = 0U;
            completionRequested = false;
            ClearTaskQueue();
            return DetachWaitQueue();
        }
    }

    /// <summary>
    /// Reuses the pipe.
    /// </summary>
    /// <remarks>
    /// The pipe can be reused only if there are no active consumers and producers.
    /// Otherwise, the behavior of the pipe is unspecified.
    /// </remarks>
    public void Reset() => ResetCore()?.TrySetResultAndSentinelToAll(result: false);

    internal ValueTask<bool> TryDequeue(out T? task, CancellationToken token)
    {
        ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;

        lock (SyncRoot)
        {
            if (TryDequeueCompletedTask(out task))
                return new(true);

            if (IsCompleted)
                return new(false);

            factory = EnqueueNode();
        }

        return factory.Invoke(token);
    }

    /// <summary>
    /// Attempts to read the completed task synchronously.
    /// </summary>
    /// <param name="task">The completed task.</param>
    /// <returns><see langword="true"/> if a task was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead([NotNullWhen(true)] out T? task)
    {
        lock (SyncRoot)
        {
            return TryDequeueCompletedTask(out task);
        }
    }

    /// <summary>
    /// Waits for the first completed task.
    /// </summary>
    /// <param name="timeout">The time to wait for the task completion.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if data is available to read; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    public ValueTask<bool> WaitToReadAsync(TimeSpan timeout, CancellationToken token = default)
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
                task = new(Volatile.Read(ref firstTask) is not null);
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
                    if (firstTask is not null)
                    {
                        task = new(true);
                        break;
                    }

                    if (IsCompleted)
                    {
                        task = new(false);
                        break;
                    }

                    factory = EnqueueNode();
                }

                task = factory.Invoke(timeout, token);
                break;
        }

        return task;
    }

    /// <summary>
    /// Waits for the first completed task.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if data is available to read; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> WaitToReadAsync(CancellationToken token = default)
        => WaitToReadAsync(new(Timeout.InfiniteTicks), token);

    /// <summary>
    /// Gets the enumerator to get the completed tasks.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The enumerator over completed tasks.</returns>
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
    {
        while (await TryDequeue(out var task, token).ConfigureAwait(false))
        {
            if (task is not null)
            {
                Debug.Assert(task.IsCompleted);

                yield return task;
            }
        }
    }
}