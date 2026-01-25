using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

/// <summary>
/// Represents reusable timeout tracker that is linked to the root cancellation token.
/// </summary>
/// <remarks>
/// The tracker can be reused multiple times to reduce the memory allocation until it gets timed out or canceled.
/// </remarks>
public sealed class TimeoutSource : IDisposable, IAsyncDisposable
{
    private readonly ITimer timer;
    private readonly CancellationTokenSource source;
    private readonly CancellationTokenRegistration rootRegistration;
    private volatile ObjectState state;

    /// <summary>
    /// Initializes a new timeout provider.
    /// </summary>
    /// <param name="provider">The time provider.</param>
    /// <param name="token">The token that can be used to cancel <see cref="Token"/>.</param>
    public TimeoutSource(TimeProvider provider, CancellationToken token)
    {
        source = new();
        Token = source.Token;
        timer = provider.CreateTimer(OnTimeout, this, InfiniteTimeSpan, InfiniteTimeSpan);
        
        Volatile.WriteBarrier();
        rootRegistration = token.UnsafeRegister(OnCanceled, this);

        static void OnCanceled(object? state)
        {
            Debug.Assert(state is TimeoutSource);
            
            Unsafe.As<TimeoutSource>(state).OnCanceled();
        }
        
        static void OnTimeout(object? state)
        {
            Debug.Assert(state is TimeoutSource);
            
            Unsafe.As<TimeoutSource>(state).OnTimeout();
        }
    }

    private void OnCanceled()
    {
        for (ObjectState current = state, tmp;; current = tmp)
        {
            if (current <= ObjectState.Started)
            {
                tmp = Interlocked.CompareExchange(ref state, ObjectState.Canceled, current);
                
                if (tmp != current)
                    continue;

                source.Cancel(throwOnFirstException: false);
            }
            
            break;
        }
    }

    private void OnTimeout()
    {
        if (Interlocked.CompareExchange(ref state, ObjectState.TimedOut, ObjectState.Started) is ObjectState.Started)
        {
            source.Cancel(throwOnFirstException: false);
        }
    }

    /// <summary>
    /// Starts the timer.
    /// </summary>
    /// <remarks>
    /// When the timer expires, it turns <see cref="Token"/> to the canceled state.
    /// If this method returns <see langword="false"/>, <see cref="Token"/> is in canceled state (or gets canceled soon).
    /// However, you can check <see cref="IsCanceled"/> or <see cref="IsTimedOut"/> to find out the state of the provider.
    /// </remarks>
    /// <param name="value">The timeout value.</param>
    /// <returns><see langword="true"/> if the timer is started successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryStart(TimeSpan value)
    {
        Timeout.Validate(value);

        switch (Interlocked.CompareExchange(ref state, ObjectState.Started, ObjectState.Initial))
        {
            case ObjectState.Initial:
                timer.Change(value, InfiniteTimeSpan);
                return true;
            case ObjectState.Disposed:
                throw new ObjectDisposedException(GetType().Name);
            default:
                return false;
        }
    }

    /// <summary>
    /// Tries to reset the timer.
    /// </summary>
    /// <returns><see langword="true"/> if this object can be reused by calling <see cref="TryStart"/> again; otherwise, <see langword="false"/>.</returns>
    public bool TryReset() => Interlocked.CompareExchange(ref state, ObjectState.Initial, ObjectState.Started) switch
    {
        ObjectState.Initial => true,
        ObjectState.Started => timer.Change(InfiniteTimeSpan, InfiniteTimeSpan),
        _ => false,
    };
    
    /// <summary>
    /// The cancellation token that is linked to the root token and the timeout.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Gets the root token passed to <seealso cref="TimeoutSource(TimeProvider, CancellationToken)"/> constructor.
    /// </summary>
    public CancellationToken RootToken => rootRegistration.Token;

    /// <summary>
    /// Gets a value indicating that this source is canceled by the root token.
    /// </summary>
    public bool IsCanceled => state is ObjectState.Canceled;

    /// <summary>
    /// Gets a value indicating that this source is timed out.
    /// </summary>
    public bool IsTimedOut => state is ObjectState.TimedOut;

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref state, ObjectState.Disposed) is not ObjectState.Disposed)
        {
            if (disposing)
            {
                rootRegistration.Dispose();
                timer.Dispose();
                source.Dispose();
            }
        }
    }

    /// <summary>
    /// Disposes the provider.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeImplAsync()
    {
        await rootRegistration.DisposeAsync().ConfigureAwait(false);
        await timer.DisposeAsync().ConfigureAwait(false);
        source.Dispose();
    }

    /// <summary>
    /// Disposes the provider asynchronously.
    /// </summary>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    public ValueTask DisposeAsync()
        => Interlocked.Exchange(ref state, ObjectState.Disposed) is not ObjectState.Disposed
            ? DisposeImplAsync()
            : ValueTask.CompletedTask;

    /// <inheritdoc />
    ~TimeoutSource() => Dispose(disposing: false);
    
    private enum ObjectState : uint
    {
        Initial = 0U,
        Started = 1U,
        Canceled = 2U,  // final state
        TimedOut = 3U,  // final state
        Disposed = 4U,  // final state
    }
}