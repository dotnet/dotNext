using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

using Diagnostics;
using static Tasks.Conversion;

/// <summary>
/// Represents a collection of asynchronous events.
/// </summary>
public class AsyncEventHub : Disposable
{
    [StructLayout(LayoutKind.Auto)]
    private struct EventSource
    {
        private readonly IEquatable<int> index;
        private TaskCompletionSource source;

        internal EventSource(int index)
        {
            this.index = index;
            source = new(this.index, TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal static int GetIndex(Task task) => (int)task.AsyncState!;

        internal readonly bool TrySignal() => source.TrySetResult();

        internal readonly bool TryCancel(CancellationToken token) => source.TrySetCanceled(token);

        internal void Reset()
        {
            if (source.Task.IsCompleted)
                source = new(index, TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal readonly Task Task => source.Task;

        public static implicit operator TaskCompletionSource(in EventSource source) => source.source;
    }

    private readonly ReaderWriterLockSlim rwLock;
    private readonly EventSource[] sources;
    private readonly Converter<Task, int> indexConverter;

    /// <summary>
    /// Initializes a new collection of asynchronous events.
    /// </summary>
    /// <param name="count">The number of asynchronous events.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or equal to zero.</exception>
    public AsyncEventHub(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        rwLock = new(LockRecursionPolicy.NoRecursion);
        sources = new EventSource[count];

        for (var i = 0; i < sources.Length; i++)
            sources[i] = new(i);

        indexConverter = EventSource.GetIndex;
    }

    /// <summary>
    /// Gets the number of events.
    /// </summary>
    public int Count => sources.Length;

    private Task WaitOneCoreAsync(int eventIndex, TimeSpan timeout, CancellationToken token)
    {
        Debug.Assert((uint)eventIndex < (uint)sources.Length);

        Task result;

        var lockTaken = false;
        var start = Timestamp.Current;
        try
        {
            lockTaken = rwLock.TryEnterReadLock(timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(sources), eventIndex).Task
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(timeout, token);
    }

    private Task WaitOneCoreAsync(int eventIndex, CancellationToken token)
    {
        Debug.Assert((uint)eventIndex < (uint)sources.Length);

        Task result;

        var lockTaken = false;
        try
        {
            rwLock.EnterReadLock();
            lockTaken = true;

            result = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(sources), eventIndex).Task;
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(token);
    }

    /// <summary>
    /// Waits for the event represented by the specified index.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public Task WaitOneAsync(int eventIndex, TimeSpan timeout, CancellationToken token = default)
    {
        if ((uint)eventIndex > (uint)sources.Length)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        if (IsDisposed)
            return DisposedTask;

        return timeout < TimeSpan.Zero ? WaitOneCoreAsync(eventIndex, token) : WaitOneCoreAsync(eventIndex, timeout, token);
    }

    /// <summary>
    /// Waits for the event represented by the specified index.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public Task WaitOneAsync(int eventIndex, CancellationToken token = default)
    {
        if ((uint)eventIndex > (uint)sources.Length)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        if (IsDisposed)
            return DisposedTask;

        return WaitOneCoreAsync(eventIndex, token);
    }

    /// <summary>
    /// Turns all events to non-signaled state.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void Reset()
    {
        ThrowIfDisposed();

        rwLock.EnterWriteLock();
        try
        {
            foreach (ref var source in sources.AsSpan())
                source.Reset();
        }
        catch
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Turns the specified event into the signaled state and reset all other events.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <returns><see langword="true"/> if the event turned into signaled state; <see langword="false"/> if the event is already in signaled state.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public bool ResetAndPulse(int eventIndex)
    {
        ThrowIfDisposed();

        if ((uint)eventIndex > (uint)sources.Length)
            throw new ArgumentOutOfRangeException(nameof(eventIndex));

        var result = false;
        rwLock.EnterWriteLock();
        try
        {
            for (var i = 0; i < sources.Length; i++)
            {
                ref var source = ref sources[i];

                if (i == eventIndex)
                {
                    result = source.TrySignal();
                }
                else
                {
                    source.Reset();
                }
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }

        return result;
    }

    /// <summary>
    /// Turns an event into the signaled state.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <returns><see langword="true"/> if the event turned into signaled state; <see langword="false"/> if the event is already in signaled state.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public bool Pulse(int eventIndex)
    {
        ThrowIfDisposed();

        if ((uint)eventIndex > (uint)sources.Length)
            throw new ArgumentOutOfRangeException(nameof(eventIndex));

        rwLock.EnterWriteLock();
        try
        {
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(sources), eventIndex).TrySignal();
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Turns the specified events into signaled state and reset all other events.
    /// </summary>
    /// <param name="eventIndexes">A span of event indexes.</param>
    /// <returns>The number of triggered events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public int ResetAndPulse(ReadOnlySpan<int> eventIndexes)
    {
        ThrowIfDisposed();

        var count = 0;

        rwLock.EnterWriteLock();
        try
        {
            for (var i = 0; i < sources.Length; i++)
            {
                ref var source = ref sources[i];

                if (!eventIndexes.Contains(i))
                {
                    source.Reset();
                }
                else if (source.TrySignal())
                {
                    count += 1;
                }
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }

        return count;
    }

    /// <summary>
    /// Turns the specified events into signaled state and reset all other events.
    /// </summary>
    /// <param name="eventIndexes">A span of event indexes.</param>
    /// <param name="flags">
    /// A set of event states. The value of each element will be overwritten by the method as follows:
    /// <see langword="true"/> if the corresponding event has been moved to the signaled state,
    /// or <see langword="false"/> if the event is already in signaled state.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="eventIndexes"/> is not equal to the length of <paramref name="flags"/>.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void ResetAndPulse(ReadOnlySpan<int> eventIndexes, Span<bool> flags)
    {
        ThrowIfDisposed();

        if (eventIndexes.Length != flags.Length)
            throw new ArgumentOutOfRangeException(nameof(flags));

        rwLock.EnterWriteLock();
        try
        {
            for (var i = 0; i < sources.Length; i++)
            {
                ref var source = ref sources[i];

                var index = eventIndexes.IndexOf(i);

                if (index < 0)
                {
                    source.Reset();
                }
                else
                {
                    Unsafe.Add(ref MemoryMarshal.GetReference(flags), index) = source.TrySignal();
                }
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Turns the specified events into signaled state.
    /// </summary>
    /// <param name="eventIndexes">A span of event indexes.</param>
    /// <returns>The number of triggered events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public int Pulse(ReadOnlySpan<int> eventIndexes)
    {
        ThrowIfDisposed();

        var count = 0;

        if (eventIndexes.IsEmpty)
            goto exit;

        rwLock.EnterWriteLock();
        try
        {
            foreach (var index in eventIndexes)
            {
                if (sources[index].TrySignal())
                    count += 1;
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }

    exit:
        return count;
    }

    /// <summary>
    /// Turns the specified events into signaled state.
    /// </summary>
    /// <param name="eventIndexes">A span of event indexes.</param>
    /// <param name="flags">
    /// A set of event states. The value of each element will be overwritten by the method as follows:
    /// <see langword="true"/> if the corresponding event has been moved to the signaled state,
    /// or <see langword="false"/> if the event is already in signaled state.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="eventIndexes"/> is not equal to the length of <paramref name="flags"/>.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void Pulse(ReadOnlySpan<int> eventIndexes, Span<bool> flags)
    {
        ThrowIfDisposed();

        if (eventIndexes.Length != flags.Length)
            throw new ArgumentOutOfRangeException(nameof(flags));

        if (eventIndexes.IsEmpty)
            return;

        rwLock.EnterWriteLock();
        try
        {
            foreach (var index in eventIndexes)
            {
                flags[index] = sources[index].TrySignal();
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Turns all events into the signaled state.
    /// </summary>
    /// <returns>The number of triggered events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public int PulseAll()
    {
        ThrowIfDisposed();

        var count = 0;

        rwLock.EnterReadLock();
        try
        {
            foreach (ref var source in sources.AsSpan())
            {
                if (source.TrySignal())
                    count += 1;
            }
        }
        finally
        {
            rwLock.ExitReadLock();
        }

        return count;
    }

    /// <summary>
    /// Turns all events into the signaled state.
    /// </summary>
    /// <param name="flags">
    /// A set of event states. The value of each element will be overwritten by the method as follows:
    /// <see langword="true"/> if the corresponding event has been moved to the signaled state,
    /// or <see langword="false"/> if the event is already in signaled state.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="flags"/> is less than <see cref="Count"/>.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void PulseAll(Span<bool> flags)
    {
        if (flags.Length < sources.Length)
            throw new ArgumentOutOfRangeException(nameof(flags));

        ThrowIfDisposed();

        rwLock.EnterReadLock();
        try
        {
            ref var state = ref MemoryMarshal.GetReference(flags);
            var i = 0;

            foreach (ref var source in sources.AsSpan())
                Unsafe.Add(ref state, i++) = source.TrySignal();
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    private Task[] GetTasks(ReadOnlySpan<int> eventIndexes)
    {
        var tasks = new Task[eventIndexes.Length];

        var taskIndex = 0;
        foreach (var i in eventIndexes)
            tasks[taskIndex++] = sources[i].Task;

        return tasks;
    }

    private Task[] GetTasks()
    {
        var tasks = new Task[sources.Length];

        for (var i = 0; i < sources.Length; i++)
            tasks[i] = sources[i].Task;

        return tasks;
    }

    private Task<int> WaitAnyCoreAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token)
    {
        Task<Task> result;

        var lockTaken = false;
        var start = Timestamp.Current;
        try
        {
            lockTaken = rwLock.TryEnterReadLock(timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Task.WhenAny(GetTasks(eventIndexes))
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException<Task>(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(timeout, token).Convert(indexConverter);
    }

    private Task<int> WaitAnyCoreAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token)
    {
        Task<Task> result;

        var lockTaken = false;
        try
        {
            rwLock.EnterReadLock();
            lockTaken = true;

            result = Task.WhenAny(GetTasks(eventIndexes));
        }
        catch (Exception e)
        {
            result = Task.FromException<Task>(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(token).Convert(indexConverter);
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="eventIndexes">A set of event indexes to wait for.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndexes"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token = default)
    {
        if (IsDisposed)
            return GetDisposedTask<int>();

        if (eventIndexes.IsEmpty)
            return Task.FromException<int>(new ArgumentOutOfRangeException(nameof(eventIndexes)));

        return timeout < TimeSpan.Zero ? WaitAnyCoreAsync(eventIndexes, token) : WaitAnyCoreAsync(eventIndexes, timeout, token);
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="eventIndexes">A set of event indexes to wait for.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndexes"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token = default)
    {
        if (IsDisposed)
            return GetDisposedTask<int>();

        if (eventIndexes.IsEmpty)
            return Task.FromException<int>(new ArgumentOutOfRangeException(nameof(eventIndexes)));

        return WaitAnyCoreAsync(eventIndexes, token);
    }

    private Task<int> WaitAnyCoreAsync(TimeSpan timeout, CancellationToken token)
    {
        Task<Task> result;

        var lockTaken = false;
        var start = Timestamp.Current;
        try
        {
            lockTaken = rwLock.TryEnterReadLock(timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Task.WhenAny(GetTasks())
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException<Task>(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(timeout, token).Convert(indexConverter);
    }

    private Task<int> WaitAnyCoreAsync(CancellationToken token)
    {
        Task<Task> result;

        var lockTaken = false;
        try
        {
            rwLock.EnterReadLock();
            lockTaken = true;

            result = Task.WhenAny(GetTasks());
        }
        catch (Exception e)
        {
            result = Task.FromException<Task>(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(token).Convert(indexConverter);
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(TimeSpan timeout, CancellationToken token = default)
    {
        if (IsDisposed)
            return GetDisposedTask<int>();

        return timeout < TimeSpan.Zero ? WaitAnyCoreAsync(token) : WaitAnyCoreAsync(timeout, token);
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(CancellationToken token = default)
        => IsDisposed ? GetDisposedTask<int>() : WaitAnyCoreAsync(token);

    private Task WaitAllCoreAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        try
        {
            rwLock.EnterReadLock();
            lockTaken = true;

            result = Task.WhenAll(GetTasks(eventIndexes));
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(token);
    }

    private Task WaitAllCoreAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        var start = Timestamp.Current;
        try
        {
            lockTaken = rwLock.TryEnterReadLock(timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Task.WhenAll(GetTasks(eventIndexes))
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="eventIndexes">The indexes of the events.</param>
    /// <param name="timeout">The time to wait for the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all of the specified events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token = default)
    {
        if (IsDisposed)
            return DisposedTask;

        if (eventIndexes.IsEmpty)
            return Task.CompletedTask;

        return timeout < TimeSpan.Zero ? WaitAllCoreAsync(eventIndexes, token) : WaitAllCoreAsync(eventIndexes, timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="eventIndexes">The indexes of the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all of the specified events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token = default)
    {
        if (IsDisposed)
            return DisposedTask;

        if (eventIndexes.IsEmpty)
            return Task.CompletedTask;

        return WaitAllCoreAsync(eventIndexes, token);
    }

    private Task WaitAllCoreAsync(CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        try
        {
            rwLock.EnterReadLock();
            lockTaken = true;

            result = Task.WhenAll(GetTasks());
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(token);
    }

    private Task WaitAllCoreAsync(TimeSpan timeout, CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        var start = Timestamp.Current;
        try
        {
            lockTaken = rwLock.TryEnterReadLock(timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Task.WhenAll(GetTasks())
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                rwLock.ExitReadLock();
        }

        return result.WaitAsync(timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="timeout">The time to wait for the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all of the specified events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(TimeSpan timeout, CancellationToken token = default)
    {
        if (IsDisposed)
            return DisposedTask;

        return timeout < TimeSpan.Zero ? WaitAllCoreAsync(token) : WaitAllCoreAsync(timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all of the specified events.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(CancellationToken token = default)
        => IsDisposed ? DisposedTask : WaitAllCoreAsync(token);

    /// <summary>
    /// Cancels all suspended callers.
    /// </summary>
    /// <param name="token">The token in canceled state.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not in canceled state.</exception>
    public void CancelSuspendedCallers(CancellationToken token)
    {
        ThrowIfDisposed();

        if (!token.IsCancellationRequested)
            throw new ArgumentOutOfRangeException(nameof(token));

        rwLock.EnterReadLock();
        try
        {
            foreach (ref var source in sources.AsSpan())
                source.TryCancel(token);
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            rwLock.Dispose();
            foreach (ref var source in sources.AsSpan())
                TrySetDisposedException(source);
        }

        base.Dispose(disposing);
    }
}