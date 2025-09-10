using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Timestamp = Diagnostics.Timestamp;

/// <summary>
/// Helps to compute timeout for asynchronous operations.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Timeout
{
    /// <summary>
    /// Represents a number of ticks in <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.
    /// </summary>
    public const long InfiniteTicks = System.Threading.Timeout.Infinite * TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Represents maximum possible timeout value, in ticks, that can be passed to
    /// some methods such as <see cref="Task.Delay(TimeSpan)"/> or <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>.
    /// </summary>
    public const long MaxTimeoutParameterTicks = int.MaxValue * TimeSpan.TicksPerMillisecond;

    private readonly Timestamp created; // IsEmpty means infinite timeout
    private readonly TimeSpan timeout;

    /// <summary>
    /// Gets infinite timeout.
    /// </summary>
    public static Timeout Infinite => default;

    /// <summary>
    /// Gets expired timeout.
    /// </summary>
    public static Timeout Expired { get; } = new(TimeSpan.Zero);

    /// <summary>
    /// Constructs a new timeout control object.
    /// </summary>
    /// <param name="timeout">Max duration of operation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    public Timeout(TimeSpan timeout)
        : this(timeout, TimeProvider.System)
    {
    }

    /// <summary>
    /// Constructs a new timeout control object.
    /// </summary>
    /// <param name="timeout">Max duration of operation.</param>
    /// <param name="provider">Time provider.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    public Timeout(TimeSpan timeout, TimeProvider provider)
    {
        switch (timeout.Ticks)
        {
            case InfiniteTicks:
                this = default;
                break;
            case < 0L:
                throw new ArgumentOutOfRangeException(nameof(timeout));
            default:
                created = new(provider);
                this.timeout = timeout;
                break;
        }
    }

    /// <summary>
    /// Constructs a new timeout control object.
    /// </summary>
    /// <param name="timeout">Max duration of operation.</param>
    /// <param name="startedAt">The point in time when operation was started.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative; or <paramref name="startedAt"/> is empty.</exception>
    public Timeout(TimeSpan timeout, Timestamp startedAt)
    {
        if (startedAt.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(startedAt));
        
        switch (timeout.Ticks)
        {
            case InfiniteTicks:
                this = default;
                break;
            case < 0L:
                throw new ArgumentOutOfRangeException(nameof(timeout));
            default:
                created = startedAt;
                this.timeout = timeout;
                break;
        }
    }

    /// <summary>
    /// Gets value of this timeout.
    /// </summary>
    public TimeSpan Value => IsInfinite ? new(InfiniteTicks) : timeout;

    /// <summary>
    /// Determines whether this timeout is infinite.
    /// </summary>
    public bool IsInfinite => created.IsEmpty;

    /// <summary>
    /// Indicates that timeout is occurred.
    /// </summary>
    public bool IsExpired => !IsInfinite && created.Elapsed > timeout;

    /// <summary>
    /// Throws <see cref="TimeoutException"/> if timeout occurs.
    /// </summary>
    /// <exception cref="TimeoutException">Timeout occurred.</exception>
    public void ThrowIfExpired() => ThrowIfExpired(TimeProvider.System);

    /// <summary>
    /// Throws <see cref="TimeoutException"/> if timeout occurs.
    /// </summary>
    /// <exception cref="TimeoutException">Timeout occurred.</exception>
    public void ThrowIfExpired(TimeProvider provider)
    {
        if (!IsInfinite && created.GetElapsedTime(provider) > timeout)
            throw new TimeoutException();
    }

    /// <summary>
    /// Throws <see cref="TimeoutException"/> if timeout occurs.
    /// </summary>
    /// <param name="remainingTime">The remaining time before timeout.</param>
    /// <exception cref="TimeoutException">Timeout occurred.</exception>
    public void ThrowIfExpired(out TimeSpan remainingTime) => ThrowIfExpired(TimeProvider.System, out remainingTime);
    
    /// <summary>
    /// Throws <see cref="TimeoutException"/> if timeout occurs.
    /// </summary>
    /// <param name="provider">Time provider.</param>
    /// <param name="remainingTime">The remaining time before timeout.</param>
    /// <exception cref="TimeoutException">Timeout occurred.</exception>
    public void ThrowIfExpired(TimeProvider provider, out TimeSpan remainingTime)
    {
        if (TryGetRemainingTime(provider, out remainingTime) is false)
            throw new TimeoutException();
    }

    /// <summary>
    /// Gets the remaining time.
    /// </summary>
    /// <param name="remainingTime">The remaining time before timeout.</param>
    /// <returns><see langword="true"/> if timeout hasn't happened yet; otherwise, <see langword="false"/>.</returns>
    public bool TryGetRemainingTime(out TimeSpan remainingTime) => TryGetRemainingTime(TimeProvider.System, out remainingTime);

    /// <summary>
    /// Gets the remaining time.
    /// </summary>
    /// <param name="provider">Time provider.</param>
    /// <param name="remainingTime">The remaining time before timeout.</param>
    /// <returns><see langword="true"/> if timeout hasn't happened yet; otherwise, <see langword="false"/>.</returns>
    public bool TryGetRemainingTime(TimeProvider provider, out TimeSpan remainingTime)
    {
        if (IsInfinite)
        {
            remainingTime = new(InfiniteTicks);
            return true;
        }

        return (remainingTime = timeout - created.GetElapsedTime(provider)) >= TimeSpan.Zero;
    }

    /// <summary>
    /// Indicates that timeout is reached.
    /// </summary>
    /// <param name="timeout">Timeout control object.</param>
    /// <returns><see langword="true"/>, if timeout is reached; otherwise, <see langword="false"/>.</returns>
    public static bool operator true(in Timeout timeout) => timeout.IsExpired;

    /// <summary>
    /// Indicates that timeout is not reached.
    /// </summary>
    /// <param name="timeout">Timeout control object.</param>
    /// <returns><see langword="false"/>, if timeout is not reached; otherwise, <see langword="false"/>.</returns>
    public static bool operator false(in Timeout timeout) => !timeout.IsExpired;

    /// <summary>
    /// Extracts original timeout value from this object.
    /// </summary>
    /// <param name="timeout">Timeout control object.</param>
    /// <returns>The original timeout value.</returns>
    public static implicit operator TimeSpan(in Timeout timeout) => timeout.Value;

    /// <summary>
    /// Validates the timeout.
    /// </summary>
    /// <param name="timeout">The timeout value.</param>
    /// <param name="parameterName">The name of the timeout parameter passed by the caller.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative and not <see cref="InfiniteTicks"/>; or greater than <see cref="MaxTimeoutParameterTicks"/>.</exception>
    public static void Validate(TimeSpan timeout, [CallerArgumentExpression(nameof(timeout))] string? parameterName = null)
    {
        if (timeout is { Ticks: < 0L and not InfiniteTicks or > MaxTimeoutParameterTicks })
            Throw(parameterName);

        [DoesNotReturn]
        [StackTraceHidden]
        static void Throw(string? parameterName)
            => throw new ArgumentOutOfRangeException(parameterName);
    }
}