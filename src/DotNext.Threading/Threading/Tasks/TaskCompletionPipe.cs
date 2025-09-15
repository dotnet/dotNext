using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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

    // if null then completion not enabled
    private TaskCompletionSource? completedAll;

    /// <summary>
    /// Initializes a new pipe.
    /// </summary>
    public TaskCompletionPipe() => pool = new();

    /// <summary>
    /// Gets or sets a value indicating that this pipe supports <see cref="Completion"/> property.
    /// </summary>
    public bool IsCompletionTaskSupported
    {
        get => Volatile.Read(in completedAll) is not null;
        init => completedAll = value ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
    }

    /// <summary>
    /// Gets a task that turns into completed state when all submitted tasks are completed.
    /// </summary>
    /// <exception cref="NotSupportedException"><see cref="IsCompletionTaskSupported"/> is <see langword="false"/>.</exception>
    /// <exception cref="PendingTaskInterruptedException"><see cref="Reset()"/> was called before completion.</exception>
    public Task Completion => Volatile.Read(in completedAll)?.Task ?? Task.FromException(new NotSupportedException());

    private object SyncRoot => this;

    private void OnCompleted(Signal signal)
    {
        if (signal.NeedsRemoval)
        {
            lock (SyncRoot)
            {
                waitQueue.Remove(signal);
            }
        }

        if (signal.TryReset(out _))
        {
            lock (SyncRoot)
            {
                pool.Return(signal);
            }
        }
    }

    /// <summary>
    /// Marks the pipe as being complete, meaning no more items will be added to it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The pipe is already completed.</exception>
    public void Complete()
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (completionRequested)
                throw new InvalidOperationException();

            completionRequested = true;
            if (scheduledTasksCount is 0U)
            {
                completedAll?.TrySetResult();
                suspendedCallers = DetachWaitQueue()?.SetResult(false);
            }
            else
            {
                suspendedCallers = null;
            }
        }

        suspendedCallers?.Unwind();
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool IsCompleted
    {
        get
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            return scheduledTasksCount is 0U && completionRequested;
        }
    }

    private bool TryAdd(T task, out uint currentVersion, object? userData)
    {
        bool result;
        ManualResetCompletionSource? suspendedCaller;

        lock (SyncRoot)
        {
            if (completionRequested)
                throw new InvalidOperationException();

            scheduledTasksCount++;
            currentVersion = version;
            suspendedCaller = (result = task.IsCompleted)
                ? EnqueueCompletedTask(new(task) { UserData = userData })
                : null;
        }

        // decouple producer thread from consumer thread
        suspendedCaller?.Resume();
        return result;
    }

    /// <summary>
    /// Adds the task to this pipe.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The pipe is closed.</exception>
    public void Add(T task) // TODO: Remove in future with optional param
        => Add(task, userData: null);

    /// <summary>
    /// Adds the task to this pipe.
    /// </summary>
    /// <remarks>
    /// <paramref name="userData"/> can be requested later using <see cref="TryRead(out T?, out object?)"/> method.
    /// </remarks>
    /// <param name="task">The task to add.</param>
    /// <param name="userData">Arbitrary object associated with the task.</param>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The pipe is closed.</exception>
    public void Add(T task, object? userData)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!TryAdd(task, out var expectedVersion, userData))
            EnqueueCompletion(task, expectedVersion, userData);
    }

    private void EnqueueCompletion(T task, uint expectedVersion, object? userData)
    {
        task
            .ConfigureAwait(false)
            .GetAwaiter()
            .UnsafeOnCompleted(new LazyLinkedTaskNode(task, this, expectedVersion) { UserData = userData }.Invoke);
    }

    /// <summary>
    /// Submits a group of tasks and mark this pipe as completed.
    /// </summary>
    /// <param name="tasks">A group of tasks.</param>
    /// <param name="complete"><see langword="true"/> to submit tasks and complete the pipe; <see langword="false"/> to submit tasks.</param>
    /// <param name="userData">Arbitrary object associated with the tasks.</param>
    /// <exception cref="InvalidOperationException">The pipe is closed.</exception>
    public void Add(ReadOnlySpan<T> tasks, bool complete = false, object? userData = null)
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCaller;
        lock (SyncRoot)
        {
            if (completionRequested)
                throw new InvalidOperationException();

            scheduledTasksCount += (uint)tasks.Length;
            var completionDetected = false;
            foreach (var task in tasks)
            {
                if (task.IsCompleted)
                {
                    AddCompletedTaskNode(new(task) { UserData = userData });
                    scheduledTasksCount--;
                    completionDetected = true;
                }
                else
                {
                    EnqueueCompletion(task, version, userData);
                }
            }

            completionRequested = complete;

            if (scheduledTasksCount is 0U && complete)
            {
                completedAll?.TrySetResult();
                suspendedCaller = DetachWaitQueue()?.SetResult(false);
            }
            else if (completionDetected)
            {
                suspendedCaller = TryDetachSuspendedCaller();
            }
            else
            {
                suspendedCaller = null;
            }
        }

        suspendedCaller?.Unwind();
    }

    /// <summary>
    /// Reuses the pipe.
    /// </summary>
    /// <remarks>
    /// The pipe can be reused only if there are no active consumers and producers.
    /// Otherwise, the behavior of the pipe is unspecified.
    /// </remarks>
    public void Reset()
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            version += 1U;
            scheduledTasksCount = 0U;
            completionRequested = false;
            ClearTaskQueue();
            suspendedCallers = DetachWaitQueue()?.SetResult(false);
            if (completedAll is not null)
            {
                completedAll.TrySetException(new PendingTaskInterruptedException());
                completedAll = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        suspendedCallers?.Unwind();
    }

    internal ValueTask<bool> TryDequeue(uint expectedVersion, out T? task, CancellationToken token)
    {
        ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
        ValueTask<bool> result;

        lock (SyncRoot)
        {
            if (expectedVersion != version)
            {
                task = null;
                result = new(false);
                goto exit;
            }

            if (TryDequeueCompletedTask(out task, out _))
            {
                result = new(true);
                goto exit;
            }

            if (IsCompleted)
            {
                result = new(false);
                goto exit;
            }

            factory = EnqueueNode();
        }

        result = factory.Invoke(token);

    exit:
        return result;
    }

    /// <summary>
    /// Attempts to read the completed task synchronously.
    /// </summary>
    /// <param name="task">The completed task.</param>
    /// <returns><see langword="true"/> if a task was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead([NotNullWhen(true)] out T? task)
        => TryRead(out task, out _);

    /// <summary>
    /// Attempts to read the completed task synchronously.
    /// </summary>
    /// <param name="task">The completed task.</param>
    /// <param name="userData">Arbitrary object associated with the task when it was added to the pipe.</param>
    /// <returns><see langword="true"/> if a task was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead([NotNullWhen(true)] out T? task, out object? userData)
    {
        lock (SyncRoot)
        {
            return TryDequeueCompletedTask(out task, out userData);
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
            case < 0L or > Timeout.MaxTimeoutParameterTicks:
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

    private async IAsyncEnumerator<T> GetAsyncEnumerator(uint expectedVersion, CancellationToken token)
    {
        while (await TryDequeue(expectedVersion, out var task, token).ConfigureAwait(false))
        {
            if (task is not null)
            {
                Debug.Assert(task.IsCompleted);

                yield return task;
            }
        }
    }

    /// <summary>
    /// Gets the enumerator to get the completed tasks.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The enumerator over completed tasks.</returns>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
        => GetAsyncEnumerator(Version, token);

    internal uint Version => Volatile.Read(in version);
}