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
    {
        switch (timeout.Ticks)
        {
            case InfiniteTicks:
                this = default;
                break;
            case < 0L:
                throw new ArgumentOutOfRangeException(nameof(timeout));
            default:
                created = new();
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
    public void ThrowIfExpired()
    {
        if (IsExpired)
            throw new TimeoutException();
    }

    /// <summary>
    /// Throws <see cref="TimeoutException"/> if timeout occurs.
    /// </summary>
    /// <param name="remaining">The remaining time before timeout.</param>
    public void ThrowIfExpired(out TimeSpan remaining)
    {
        if (IsInfinite)
        {
            remaining = new(InfiniteTicks);
        }
        else if ((remaining = timeout - created.Elapsed) < default(TimeSpan))
        {
            throw new TimeoutException();
        }
    }

    /// <summary>
    /// Gets the remaining time.
    /// </summary>
    /// <value>The remaining time; or <see langword="null"/> if timeout occurred.</value>
    public TimeSpan? RemainingTime
    {
        get
        {
            TimeSpan result;

            return IsInfinite
                ? new(InfiniteTicks)
                : (result = timeout - created.Elapsed) >= default(TimeSpan)
                ? result
                : null;
        }
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
}