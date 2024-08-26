using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Diagnostics;

/// <summary>
/// Represents a collection of asynchronous events.
/// </summary>
[DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
public partial class AsyncEventHub : IResettable
{
    private readonly AsyncEvent[] sources;
    private readonly List<Task<int>> buffer;

    /// <summary>
    /// Initializes a new collection of asynchronous events.
    /// </summary>
    /// <param name="count">The number of asynchronous events.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or equal to zero.</exception>
    public AsyncEventHub(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        sources = new AsyncEvent[count];

        for (var i = 0; i < sources.Length; i++)
            sources[i] = new(i);

        buffer = new(count);
    }

    private static void ResetIfNeeded(ref AsyncEvent source)
    {
        var sourceCopy = source;
        if (sourceCopy.Task.IsCompleted)
            source = sourceCopy.Reset();
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
        var start = new Timestamp();
        try
        {
            lockTaken = Monitor.TryEnter(sources, timeout);
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
                Monitor.Exit(sources);
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
            Monitor.Enter(sources, ref lockTaken);
            result = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(sources), eventIndex).Task;
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(sources);
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
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public Task WaitOneAsync(int eventIndex, TimeSpan timeout, CancellationToken token = default)
    {
        if ((uint)eventIndex >= (uint)sources.Length)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        return timeout < TimeSpan.Zero ? WaitOneCoreAsync(eventIndex, token) : WaitOneCoreAsync(eventIndex, timeout, token);
    }

    /// <summary>
    /// Waits for the event represented by the specified index.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public Task WaitOneAsync(int eventIndex, CancellationToken token = default)
        => (uint)eventIndex >= (uint)sources.Length
            ? Task.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)))
            : WaitOneCoreAsync(eventIndex, token);

    /// <summary>
    /// Turns all events to non-signaled state.
    /// </summary>
    public void Reset()
    {
        lock (sources)
        {
            foreach (ref var source in sources.AsSpan())
                ResetIfNeeded(ref source);
        }
    }

    /// <summary>
    /// Turns the specified event into the signaled state and reset all other events.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <returns><see langword="true"/> if the event turned into signaled state; <see langword="false"/> if the event is already in signaled state.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public bool ResetAndPulse(int eventIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)eventIndex, (uint)sources.Length, nameof(eventIndex));

        var result = false;
        lock (sources)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                ref var source = ref sources[i];

                if (i == eventIndex)
                {
                    result = source.TrySetResult();
                }
                else
                {
                    ResetIfNeeded(ref source);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Turns an event into the signaled state.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <returns><see langword="true"/> if the event turned into signaled state; <see langword="false"/> if the event is already in signaled state.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    public bool Pulse(int eventIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)eventIndex, (uint)sources.Length, nameof(eventIndex));

        lock (sources)
        {
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(sources), eventIndex).TrySetResult();
        }
    }

    /// <summary>
    /// Turns the specified events into signaled state and reset all other events.
    /// </summary>
    /// <param name="eventIndexes">A span of event indexes.</param>
    /// <returns>The number of triggered events.</returns>
    public int ResetAndPulse(ReadOnlySpan<int> eventIndexes)
    {
        var count = 0;

        lock (sources)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                ref var source = ref sources[i];

                if (!eventIndexes.Contains(i))
                {
                    ResetIfNeeded(ref source);
                }
                else if (source.TrySetResult())
                {
                    count += 1;
                }
            }
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
    public void ResetAndPulse(ReadOnlySpan<int> eventIndexes, Span<bool> flags)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(eventIndexes.Length, flags.Length, nameof(flags));

        lock (sources)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                ref var source = ref sources[i];

                var index = eventIndexes.IndexOf(i);

                if (index < 0)
                {
                    ResetIfNeeded(ref source);
                }
                else
                {
                    Unsafe.Add(ref MemoryMarshal.GetReference(flags), index) = source.TrySetResult();
                }
            }
        }
    }

    /// <summary>
    /// Turns the specified events into signaled state.
    /// </summary>
    /// <param name="eventIndexes">A span of event indexes.</param>
    /// <returns>The number of triggered events.</returns>
    public int Pulse(ReadOnlySpan<int> eventIndexes)
    {
        var count = 0;

        if (eventIndexes.IsEmpty)
            goto exit;

        lock (sources)
        {
            foreach (var index in eventIndexes)
            {
                if (sources[index].TrySetResult())
                    count += 1;
            }
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
    public void Pulse(ReadOnlySpan<int> eventIndexes, Span<bool> flags)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(eventIndexes.Length, flags.Length, nameof(flags));

        if (eventIndexes.IsEmpty)
            return;

        lock (sources)
        {
            foreach (var index in eventIndexes)
            {
                flags[index] = sources[index].TrySetResult();
            }
        }
    }

    /// <summary>
    /// Turns all events into the signaled state.
    /// </summary>
    /// <returns>The number of triggered events.</returns>
    public int PulseAll()
    {
        var count = 0;

        lock (sources)
        {
            foreach (var source in sources)
            {
                if (source.TrySetResult())
                    count += 1;
            }
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
    public void PulseAll(Span<bool> flags)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(flags.Length, sources.Length, nameof(flags));

        lock (sources)
        {
            ref var state = ref MemoryMarshal.GetReference(flags);

            for (var i = 0; i < sources.Length; i++)
                Unsafe.Add(ref state, i) = sources[i].TrySetResult();
        }
    }

    private List<Task<int>> GetTasks(ReadOnlySpan<int> eventIndexes)
    {
        Debug.Assert(eventIndexes is not []);
        Debug.Assert(buffer is []);

        foreach (var i in eventIndexes)
        {
            buffer.Add(sources[i].Task);
        }

        return buffer;
    }

    private List<Task<int>> GetTasks()
    {
        Debug.Assert(buffer is []);

        foreach (var source in sources)
        {
            buffer.Add(source.Task);
        }

        return buffer;
    }

    private Task<int> WaitAnyCoreAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token)
    {
        Task<Task<int>> result;

        var lockTaken = false;
        var start = new Timestamp();
        try
        {
            lockTaken = Monitor.TryEnter(sources, timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Task.WhenAny(GetTasks(eventIndexes))
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException<Task<int>>(e);
        }
        finally
        {
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.Unwrap().WaitAsync(timeout, token);
    }

    private Task<int> WaitAnyCoreAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token)
    {
        Task<Task<int>> result;

        var lockTaken = false;
        try
        {
            Monitor.Enter(sources, ref lockTaken);
            result = Task.WhenAny(GetTasks(eventIndexes));
        }
        catch (Exception e)
        {
            result = Task.FromException<Task<int>>(e);
        }
        finally
        {
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.Unwrap().WaitAsync(token);
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="eventIndexes">A set of event indexes to wait for.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndexes"/> is empty.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token = default)
    {
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
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token = default)
        => eventIndexes.IsEmpty
            ? Task.FromException<int>(new ArgumentOutOfRangeException(nameof(eventIndexes)))
            : WaitAnyCoreAsync(eventIndexes, token);

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(TimeSpan timeout, CancellationToken token = default)
        => timeout < TimeSpan.Zero ? WaitAnyAsync(token) : WaitAnyCoreAsync(timeout, token);
    
    private Task<int> WaitAnyCoreAsync(TimeSpan timeout, CancellationToken token)
    {
        Task<Task<int>> result;

        var lockTaken = false;
        var start = new Timestamp();
        try
        {
            lockTaken = Monitor.TryEnter(sources, timeout);
            result = lockTaken && (timeout -= start.Elapsed) > TimeSpan.Zero
                ? Task.WhenAny(GetTasks())
                : throw new TimeoutException();
        }
        catch (Exception e)
        {
            result = Task.FromException<Task<int>>(e);
        }
        finally
        {
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.Unwrap().WaitAsync(timeout, token);
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<int> WaitAnyAsync(CancellationToken token = default)
    {
        Task<Task<int>> result;

        var lockTaken = false;
        try
        {
            Monitor.Enter(sources, ref lockTaken);
            result = Task.WhenAny(GetTasks());
        }
        catch (Exception e)
        {
            result = Task.FromException<Task<int>>(e);
        }
        finally
        {
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.Unwrap().WaitAsync(token);
    }

    private Task WaitAllCoreAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        try
        {
            Monitor.Enter(sources, ref lockTaken);
            result = Task.WhenAll(GetTasks(eventIndexes));
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.WaitAsync(token);
    }

    private Task WaitAllCoreAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        var start = new Timestamp();
        try
        {
            lockTaken = Monitor.TryEnter(sources, timeout);
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
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.WaitAsync(timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="eventIndexes">The indexes of the events.</param>
    /// <param name="timeout">The time to wait for the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(ReadOnlySpan<int> eventIndexes, TimeSpan timeout, CancellationToken token = default)
    {
        if (eventIndexes.IsEmpty)
            return Task.CompletedTask;

        return timeout < TimeSpan.Zero ? WaitAllCoreAsync(eventIndexes, token) : WaitAllCoreAsync(eventIndexes, timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="eventIndexes">The indexes of the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(ReadOnlySpan<int> eventIndexes, CancellationToken token = default)
        => eventIndexes.IsEmpty ? Task.CompletedTask : WaitAllCoreAsync(eventIndexes, token);

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="timeout">The time to wait for the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(TimeSpan timeout, CancellationToken token = default)
        => timeout < TimeSpan.Zero ? WaitAllAsync(token) : WaitAllCoreAsync(timeout, token);
    
    private Task WaitAllCoreAsync(TimeSpan timeout, CancellationToken token)
    {
        Task result;

        var lockTaken = false;
        var start = new Timestamp();
        try
        {
            lockTaken = Monitor.TryEnter(sources, timeout);
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
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.WaitAsync(timeout, token);
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task WaitAllAsync(CancellationToken token = default)
    {
        Task result;

        var lockTaken = false;
        try
        {
            Monitor.Enter(sources, ref lockTaken);
            result = Task.WhenAll(GetTasks());
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }
        finally
        {
            buffer.Clear();
            if (lockTaken)
                Monitor.Exit(sources);
        }

        return result.WaitAsync(token);
    }

    /// <summary>
    /// Cancels all suspended callers.
    /// </summary>
    /// <param name="token">The token in canceled state.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not in canceled state.</exception>
    public void CancelSuspendedCallers(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
            throw new ArgumentOutOfRangeException(nameof(token));

        lock (sources)
        {
            foreach (var source in sources)
                source.TrySetCanceled(token);
        }
    }
    
    private sealed class AsyncEvent(int index) : TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
    {
        public bool TrySetResult() => TrySetResult(index);

        public AsyncEvent Reset() => new(index);
    }
}