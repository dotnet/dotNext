using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Diagnostics;

using Threading;

/// <summary>
/// Represents timestamp.
/// </summary>
/// <remarks>
/// This class can be used as allocation-free alternative to <see cref="System.Diagnostics.Stopwatch"/>.
/// </remarks>
public readonly record struct Timestamp :
    IComparable<Timestamp>,
    IComparisonOperators<Timestamp, Timestamp, bool>,
    IAdditionOperators<Timestamp, TimeSpan, Timestamp>,
    ISubtractionOperators<Timestamp, TimeSpan, Timestamp>,
    IInterlockedOperations<Timestamp>
{
    private static readonly double TickFrequency = GetTickFrequency(TimeProvider.System);
    private readonly long ticks;

    private Timestamp(long ticks) => this.ticks = ticks;

    /// <summary>
    /// Captures the current point in time.
    /// </summary>
    public Timestamp()
        : this(TimeProvider.System) // ensure that timestamp is not empty
    {
    }

    /// <summary>
    /// Constructs timestamp from <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="ts">The point in time.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ts"/> is negative.</exception>
    public Timestamp(TimeSpan ts)
        : this(ts >= TimeSpan.Zero ? FromTimeSpan(ts) : throw new ArgumentOutOfRangeException(nameof(ts)))
    {
    }

    /// <summary>
    /// Captures the current point in time.
    /// </summary>
    /// <param name="provider">The time provider.</param>
    public Timestamp(TimeProvider provider)
        : this(Math.Max(provider.GetTimestamp(), 1L)) // ensure that timestamp is not empty
    {
    }

    /// <summary>
    /// Gets a value indicating that the timestamp is zero.
    /// </summary>
    public bool IsEmpty => ticks is 0L;

    /// <summary>
    /// Gets a value indicating that the current timestamp represents the future point in time.
    /// </summary>
    public bool IsFuture => ticks > TimeProvider.System.GetTimestamp();

    /// <summary>
    /// Gets a value indicating that the current timestamp represents the past point in time.
    /// </summary>
    public bool IsPast => ticks < TimeProvider.System.GetTimestamp();
    
    private static double GetTickFrequency(TimeProvider provider) => (double)TimeSpan.TicksPerSecond / provider.TimestampFrequency;

    private static long ToTicks(double duration)
        => unchecked((long)(TickFrequency * duration));

    private static long ToTicks(double duration, TimeProvider provider)
        => unchecked((long)(GetTickFrequency(provider) * duration));

    private static long FromTimeSpan(TimeSpan value)
        => unchecked((long)(value.Ticks / TickFrequency));

    /// <summary>
    /// Gets <see cref="TimeSpan"/> representing this timestamp.
    /// </summary>
    /// <remarks>
    /// This property may return a value with lost precision.
    /// </remarks>
    public TimeSpan Value => new(ToTicks(ticks));

    /// <summary>
    /// Gets precise difference between the current point in time and this timestamp.
    /// </summary>
    /// <remarks>
    /// This property is always greater than or equal to <see cref="TimeSpan.Zero"/>.
    /// </remarks>
    public TimeSpan Elapsed => GetElapsedTime(TimeProvider.System);

    /// <summary>
    /// Gets precise difference between the current point in time and this timestamp.
    /// </summary>
    /// <param name="provider">Time provider.</param>
    /// <returns>The elapsed time between the starting timestamp and the time of this call.</returns>
    public TimeSpan GetElapsedTime(TimeProvider provider) => new(ToTicks(GetElapsedTicks(provider), provider));

    /// <summary>
    /// Gets the total elapsed time measured by the current instance, in timer ticks.
    /// </summary>
    public long ElapsedTicks => GetElapsedTicks(TimeProvider.System);
    
    /// <summary>
    /// Gets the total elapsed time measured by the current instance, in timer ticks.
    /// </summary>
    /// <param name="provider">Time provider.</param>
    /// <returns>The elapsed time, in ticks, between the starting timestamp and the time of this call.</returns>
    public long GetElapsedTicks(TimeProvider provider) => Math.Max(1L, provider.GetTimestamp() - ticks);

    /// <summary>
    /// Gets the total elapsed time measured by the current instance, in milliseconds.
    /// </summary>
    public double ElapsedMilliseconds => GetElapsedMilliseconds(TimeProvider.System);

    /// <summary>
    /// Gets the total elapsed time measured by the current instance, in milliseconds.
    /// </summary>
    /// <param name="provider">Time provider.</param>
    /// <returns>The elapsed time, in ms, between the starting timestamp and the time of this call.</returns>
    public double GetElapsedMilliseconds(TimeProvider provider)
        => (double)GetElapsedTicks(provider) / provider.TimestampFrequency * 1_000D;

    /// <summary>
    /// Gets a difference between two timestamps, in milliseconds.
    /// </summary>
    /// <param name="past">The timestamp in the past.</param>
    /// <returns>The number of milliseconds since <paramref name="past"/>.</returns>
    public double ElapsedSince(Timestamp past) => ElapsedSince(past, TimeProvider.System);

    /// <summary>
    /// Gets a difference between two timestamps, in milliseconds.
    /// </summary>
    /// <param name="past">The timestamp in the past.</param>
    /// <param name="provider">Time provider.</param>
    /// <returns>The number of milliseconds since <paramref name="past"/>.</returns>
    public double ElapsedSince(Timestamp past, TimeProvider provider)
        => (double)(ticks - past.ticks) / provider.TimestampFrequency * 1_000D;

    /// <summary>
    /// Gets <see cref="TimeSpan"/> representing the given timestamp.
    /// </summary>
    /// <remarks>
    /// This operation leads to loss of precision.
    /// </remarks>
    /// <param name="stamp">The timestamp to convert.</param>
    public static explicit operator TimeSpan(Timestamp stamp) => stamp.Value;

    /// <summary>
    /// Compares this timestamp with the given value.
    /// </summary>
    /// <param name="other">The timestamp to compare.</param>
    /// <returns>The result of comparison.</returns>
    public int CompareTo(Timestamp other) => ticks.CompareTo(other.ticks);

    /// <summary>
    /// Gets timestamp in the form of the string.
    /// </summary>
    /// <returns>The string representing this timestamp.</returns>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Determines whether the first timestamp is greater than the second.
    /// </summary>
    /// <param name="first">The first timestamp to compare.</param>
    /// <param name="second">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is greater than <paramref name="second"/>.</returns>
    public static bool operator >(Timestamp first, Timestamp second) => first.ticks > second.ticks;

    /// <summary>
    /// Determines whether the first timestamp is less than the second.
    /// </summary>
    /// <param name="first">The first timestamp to compare.</param>
    /// <param name="second">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is less than <paramref name="second"/>.</returns>
    public static bool operator <(Timestamp first, Timestamp second) => first.ticks < second.ticks;

    /// <summary>
    /// Determines whether the first timestamp is greater than or equal to the second.
    /// </summary>
    /// <param name="first">The first timestamp to compare.</param>
    /// <param name="second">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is greater than or equal to <paramref name="second"/>.</returns>
    public static bool operator >=(Timestamp first, Timestamp second) => first.ticks >= second.ticks;

    /// <summary>
    /// Determines whether the first timestamp is less than or equal to the second.
    /// </summary>
    /// <param name="first">The first timestamp to compare.</param>
    /// <param name="second">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is less than or equal to <paramref name="second"/>.</returns>
    public static bool operator <=(Timestamp first, Timestamp second) => first.ticks <= second.ticks;

    /// <summary>
    /// Adds the specified duration to the timestamp.
    /// </summary>
    /// <param name="x">The timestamp value.</param>
    /// <param name="y">The delta.</param>
    /// <returns>The modified timestamp.</returns>
    /// <exception cref="OverflowException"><paramref name="y"/> is too large.</exception>
    public static Timestamp operator checked +(Timestamp x, TimeSpan y)
    {
        var ticks = checked(x.ticks + FromTimeSpan(y));
        return ticks >= 0L ? new(ticks) : throw new OverflowException();
    }

    /// <summary>
    /// Adds the specified duration to the timestamp.
    /// </summary>
    /// <param name="x">The timestamp value.</param>
    /// <param name="y">The delta.</param>
    /// <returns>The modified timestamp.</returns>
    public static Timestamp operator +(Timestamp x, TimeSpan y)
    {
        var ticks = x.ticks + FromTimeSpan(y);
        return ticks > 0L ? new(ticks) : default;
    }

    /// <summary>
    /// Subtracts the specified duration from the timestamp.
    /// </summary>
    /// <param name="x">The timestamp value.</param>
    /// <param name="y">The delta.</param>
    /// <returns>The modified timestamp.</returns>
    /// <exception cref="OverflowException"><paramref name="y"/> is too large.</exception>
    public static Timestamp operator checked -(Timestamp x, TimeSpan y)
    {
        var ticks = checked(x.ticks - FromTimeSpan(y));
        return ticks >= 0L ? new(ticks) : throw new OverflowException();
    }

    /// <summary>
    /// Subtracts the specified duration from the timestamp.
    /// </summary>
    /// <param name="x">The timestamp value.</param>
    /// <param name="y">The delta.</param>
    /// <returns>The modified timestamp.</returns>
    /// <exception cref="OverflowException"><paramref name="y"/> is too large.</exception>
    public static Timestamp operator -(Timestamp x, TimeSpan y)
    {
        var ticks = x.ticks - FromTimeSpan(y);
        return ticks > 0L ? new(ticks) : default;
    }

    /// <summary>
    /// Reads the timestamp and prevents the processor from reordering memory operations.
    /// </summary>
    /// <param name="location">The managed pointer to the timestamp.</param>
    /// <returns>The value at the specified location.</returns>
    public static Timestamp VolatileRead(ref readonly Timestamp location)
        => new(Volatile.Read(in location.ticks));

    /// <summary>
    /// Writes the timestamp and prevents the processor from reordering memory operations.
    /// </summary>
    /// <param name="location">The managed pointer to the timestamp.</param>
    /// <param name="newValue">The value to write.</param>
    public static void VolatileWrite(ref Timestamp location, Timestamp newValue)
        => Volatile.Write(ref Unsafe.AsRef(in location.ticks), newValue.ticks);

    /// <summary>
    /// Updates the timestamp to the current point in time and prevents the processor from reordering memory operations.
    /// </summary>
    /// <param name="location">The location of the timestamp to update.</param>
    public static void Refresh(ref Timestamp location)
        => Refresh(ref location, TimeProvider.System);

    /// <summary>
    /// Updates the timestamp to the current point in time and prevents the processor from reordering memory operations.
    /// </summary>
    /// <param name="location">The location of the timestamp to update.</param>
    /// <param name="provider">Time provider.</param>
    public static void Refresh(ref Timestamp location, TimeProvider provider)
        => Volatile.Write(ref Unsafe.AsRef(in location.ticks), Math.Max(1L, provider.GetTimestamp()));

    /// <inheritdoc cref="IInterlockedOperations{T}.CompareExchange(ref T, T, T)"/>
    public static Timestamp CompareExchange(ref Timestamp location, Timestamp value, Timestamp comparand)
        => new(Interlocked.CompareExchange(ref Unsafe.AsRef(in location.ticks), value.ticks, comparand.ticks));
}