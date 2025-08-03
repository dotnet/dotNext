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
    private const uint InitialState = 0U;
    private const uint StartedState = 1U;
    private const uint CanceledState = 2U; // final state
    private const uint TimedOutState = 3U; // final state
    private const uint DisposedState = 4U; // final state

    private readonly ITimer timer;
    private readonly CancellationTokenSource source;
    private readonly CancellationTokenRegistration rootRegistration;
    private volatile uint state;

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
        
        Interlocked.MemoryBarrier();
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
        for (uint current = state, tmp;; current = tmp)
        {
            if (current <= StartedState)
            {
                tmp = Interlocked.CompareExchange(ref state, CanceledState, current);
                
                if (tmp != current)
                    continue;

                source.Cancel(throwOnFirstException: false);
            }
            
            break;
        }
    }

    private void OnTimeout()
    {
        if (Interlocked.CompareExchange(ref state, TimedOutState, StartedState) is StartedState)
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

        switch (Interlocked.CompareExchange(ref state, StartedState, InitialState))
        {
            case InitialState:
                timer.Change(value, InfiniteTimeSpan);
                return true;
            case DisposedState:
                throw new ObjectDisposedException(GetType().Name);
            default:
                return false;
        }
    }

    /// <summary>
    /// Tries to reset the timer.
    /// </summary>
    /// <returns><see langword="true"/> if this object can be reused by calling <see cref="TryStart"/> again; otherwise, <see langword="false"/>.</returns>
    public bool TryReset() => Interlocked.CompareExchange(ref state, InitialState, StartedState) switch
    {
        InitialState => true,
        StartedState => timer.Change(InfiniteTimeSpan, InfiniteTimeSpan),
        _ => false,
    };
    
    /// <summary>
    /// The cancellation token that is linked to the root token and the timeout.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Gets a value indicating that this source is canceled by the root token.
    /// </summary>
    public bool IsCanceled => state is CanceledState;

    /// <summary>
    /// Gets a value indicating that this source is timed out.
    /// </summary>
    public bool IsTimedOut => state is TimedOutState;

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref state, DisposedState) is not DisposedState)
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
        => Interlocked.Exchange(ref state, DisposedState) is not DisposedState
            ? DisposeImplAsync()
            : ValueTask.CompletedTask;

    /// <inheritdoc />
    ~TimeoutSource() => Dispose(disposing: false);
}