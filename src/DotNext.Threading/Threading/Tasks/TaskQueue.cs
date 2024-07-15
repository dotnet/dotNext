using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents a queue of scheduled tasks.
/// </summary>
/// <remarks>
/// The queue returns tasks in the order as they added (FIFO) in contrast
/// to <see cref="TaskCompletionPipe{T}"/>.
/// </remarks>
/// <typeparam name="T">The type of tasks in the queue.</typeparam>
public class TaskQueue<T> : IAsyncEnumerable<T>, IResettable
    where T : Task
{
    private readonly T?[] array;
    private int tail, head, count;
    private Signal? signal;

    /// <summary>
    /// Initializes a new empty queue.
    /// </summary>
    /// <param name="capacity">The maximum number of tasks in the queue.</param>
    public TaskQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        array = new T[capacity];
    }

    private ref T? this[int index]
    {
        get
        {
            Debug.Assert((uint)index < (uint)array.Length);

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
        }
    }

    /// <summary>
    /// Gets a head of this queue.
    /// </summary>
    public T? HeadTask
    {
        get
        {
            lock (array)
            {
                return count > 0 ? this[head] : null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChangeCount([ConstantExpected] bool increment)
    {
        Debug.Assert(Monitor.IsEntered(array));

        count += increment ? +1 : -1;
        if (signal?.TrySetResult() ?? false)
            signal = null;
    }

    private void MoveNext(ref int index)
    {
        Debug.Assert(Monitor.IsEntered(array));

        var value = index + 1;
        index = value == array.Length ? 0 : value;
    }

    /// <summary>
    /// Gets a value indicating that the queue has free space to place a task.
    /// </summary>
    public bool CanEnqueue
    {
        get
        {
            lock (array)
            {
                return count < array.Length;
            }
        }
    }

    /// <summary>
    /// Ensures that the queue has free space to enqueue a task.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask EnsureFreeSpaceAsync(CancellationToken token = default)
    {
        Task task;
        lock (array)
        {
            if (count < array.Length)
            {
                task = Task.CompletedTask;
            }
            else
            {
                signal ??= new();
                task = signal.Task;
            }
        }

        return new(task.WaitAsync(token));
    }

    /// <summary>
    /// Tries to enqueue the task.
    /// </summary>
    /// <param name="task">The task to enqueue.</param>
    /// <returns><see langword="true"/> if the task is enqueued successfully; <see langword="false"/> if this queue is full.</returns>
    public bool TryEnqueue(T task)
    {
        ArgumentNullException.ThrowIfNull(task);

        bool result;
        lock (array)
        {
            if (result = count < array.Length)
            {
                this[tail] = task;
                MoveNext(ref tail);
                ChangeCount(increment: true);
            }
        }

        return result;
    }

    private bool TryEnqueue(T task, out Task waitTask)
    {
        bool result;
        lock (array)
        {
            if (result = count < array.Length)
            {
                this[tail] = task;
                MoveNext(ref tail);
                ChangeCount(increment: true);
                waitTask = Task.CompletedTask;
            }
            else
            {
                signal ??= new();
                waitTask = signal.Task;
            }
        }

        return result;
    }

    /// <summary>
    /// Enqueues the task.
    /// </summary>
    /// <remarks>
    /// The caller suspends if the queue is full.
    /// </remarks>
    /// <param name="task">The task to enqueue.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask EnqueueAsync(T task, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        while (!TryEnqueue(task, out var waitTask))
        {
            await waitTask.WaitAsync(token).ConfigureAwait(false);
        }
    }

    private T? TryPeekOrDequeue(out int head, out Task enqueueTask, out bool completed)
    {
        T? result;
        lock (array)
        {
            if (count > 0)
            {
                result = this[head = this.head];
                enqueueTask = Task.CompletedTask;
                if (completed = result is { IsCompleted: true })
                {
                    MoveNext(ref head);
                    ChangeCount(increment: false);
                }
            }
            else
            {
                head = default;
                result = null;
                completed = default;
                signal ??= new();
                enqueueTask = signal.Task;
            }
        }

        return result;
    }

    private bool TryDequeue(int expectedHead, T task)
    {
        bool result;
        lock (array)
        {
            ref var element = ref this[expectedHead];
            if (result = count > 0 && head == expectedHead && ReferenceEquals(element, task))
            {
                MoveNext(ref head);
                element = null;
                ChangeCount(increment: false);
            }
        }

        return result;
    }

    /// <summary>
    /// Tries to dequeue the completed task.
    /// </summary>
    /// <param name="task">The completed task.</param>
    /// <returns><see langword="true"/> if <paramref name="task"/> is completed; otherwise, <see langword="false"/>.</returns>
    public bool TryDequeue([NotNullWhen(true)] out T? task)
    {
        lock (array)
        {
            ref var element = ref this[head];
            task = element;
            if (count > 0 && task is { IsCompleted: true })
            {
                MoveNext(ref head);
                element = null;
                ChangeCount(increment: false);
            }
            else
            {
                task = null;
            }
        }

        return task is not null;
    }

    /// <summary>
    /// Dequeues the task asynchronously.
    /// </summary>
    /// <remarks>The caller suspends if the queue is empty.</remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The completed task.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask<T> DequeueAsync(CancellationToken token = default)
    {
        for (var filter = token.CanBeCanceled ? null : Predicate.Constant<Exception>(true);;)
        {
            if (TryPeekOrDequeue(out var expectedHead, out var enqueueTask, out var completed) is not { } task)
            {
                await enqueueTask.WaitAsync(token).ConfigureAwait(false);
                continue;
            }

            if (!completed)
            {
                await task.WaitAsync(token).SuspendException(filter ??= token.SuspendAllExceptCancellation).ConfigureAwait(false);

                if (!TryDequeue(expectedHead, task))
                    continue;
            }

            return task;
        }
    }

    /// <summary>
    /// Tries to dequeue the completed task.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The completed task; or <see langword="null"/> if the queue is empty.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask<T?> TryDequeueAsync(CancellationToken token = default)
    {
        for (var filter = token.CanBeCanceled ? null : Predicate.Constant<Exception>(true);;)
        {
            T? task;
            if ((task = TryPeekOrDequeue(out var expectedHead, out _, out var completed)) is not null && !completed)
            {
                await task.WaitAsync(token).SuspendException(filter ??= token.SuspendAllExceptCancellation).ConfigureAwait(false);

                if (!TryDequeue(expectedHead, task))
                    continue;
            }

            return task;
        }
    }

    /// <summary>
    /// Gets consuming enumerator over tasks in the queue.
    /// </summary>
    /// <remarks>
    /// The enumerator stops if the queue is empty.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The enumerator over completed tasks.</returns>
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
    {
        for (var filter = token.CanBeCanceled ? null : Predicate.Constant<Exception>(true);
             TryPeekOrDequeue(out var expectedHead, out _, out var completed) is { } task;)
        {
            if (!completed)
            {
                await task.WaitAsync(token).SuspendException(filter ??= token.SuspendAllExceptCancellation).ConfigureAwait(false);
                if (!TryDequeue(expectedHead, task))
                    continue;
            }

            yield return task;
        }
    }

    /// <summary>
    /// Clears the queue.
    /// </summary>
    public void Clear()
    {
        lock (array)
        {
            head = tail = count = 0;
            Array.Clear(array);
            if (signal?.TrySetResult() ?? false)
                signal = null;
        }
    }

    /// <inheritdoc cref="Clear()"/>
    void IResettable.Reset() => Clear();

    private sealed class Signal() : TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
}

file static class CancellationTokenExtensions
{
    internal static bool SuspendAllExceptCancellation(this object token, Exception e)
        => e is not OperationCanceledException canceledEx || !canceledEx.CancellationToken.Equals(token);
}