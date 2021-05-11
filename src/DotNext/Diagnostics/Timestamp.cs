using System;
using System.Runtime.CompilerServices;
using System.Threading;
using static System.Diagnostics.Stopwatch;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Diagnostics
{
    /// <summary>
    /// Represents timestamp.
    /// </summary>
    /// <remarks>
    /// This class can be used as allocation-free alternative to <see cref="System.Diagnostics.Stopwatch"/>.
    /// </remarks>
    public readonly struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>
    {
        private readonly long ticks;

        private Timestamp(long ticks) => this.ticks = ticks;

        /// <summary>
        /// Gets the current point in time.
        /// </summary>
        public static Timestamp Current => new(GetTimestamp());

        private static long ToTicks(double duration)
            => (long)(TimeSpan.TicksPerSecond * duration / Frequency);

        /// <summary>
        /// Gets <see cref="TimeSpan"/> representing this timestamp.
        /// </summary>
        public TimeSpan Value => new(ToTicks(ticks));

        /// <summary>
        /// Gets precise difference between the current point in time and this timestamp.
        /// </summary>
        public TimeSpan Elapsed => new(ToTicks(Math.Max(0L, GetTimestamp() - ticks)));

        /// <summary>
        /// Gets <see cref="TimeSpan"/> representing the given timestamp.
        /// </summary>
        /// <param name="stamp">The timestamp to convert.</param>
        public static implicit operator TimeSpan(Timestamp stamp) => stamp.Value;

        /// <summary>
        /// Determines whether the current timestamp equals to the specified timestamp.
        /// </summary>
        /// <param name="other">The timestamp to compare.</param>
        /// <returns><see langword="true"/> if this timestamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Timestamp other) => ticks == other.ticks;

        /// <summary>
        /// Compares this timestamp with the given value.
        /// </summary>
        /// <param name="other">The timestamp to compare.</param>
        /// <returns>The result of comparison.</returns>
        public int CompareTo(Timestamp other) => ticks.CompareTo(other.ticks);

        /// <summary>
        /// Determines whether the current timestamp equals to the specified timestamp.
        /// </summary>
        /// <param name="other">The timestamp to compare.</param>
        /// <returns><see langword="true"/> if this timestamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is Timestamp stamp && Equals(stamp);

        /// <summary>
        /// Computes hash code for this timestamp.
        /// </summary>
        /// <returns>The hash code of this timestamp.</returns>
        public override int GetHashCode() => ticks.GetHashCode();

        /// <summary>
        /// Gets timestamp in the form of the string.
        /// </summary>
        /// <returns>The string representing this timestamp.</returns>
        public override string ToString() => Value.ToString();

        /// <summary>
        /// Determines whether the two timestamps are equal.
        /// </summary>
        /// <param name="first">The first timestamp to compare.</param>
        /// <param name="second">The second timestamp to compare.</param>
        /// <returns><see langword="true"/> if both timestamps are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Timestamp first, Timestamp second) => first.ticks == second.ticks;

        /// <summary>
        /// Determines whether the two timestamps are equal.
        /// </summary>
        /// <param name="first">The first timestamp to compare.</param>
        /// <param name="second">The second timestamp to compare.</param>
        /// <returns><see langword="false"/> if both timestamps are equal; otherwise, <see langword="true"/>.</returns>
        public static bool operator !=(Timestamp first, Timestamp second) => first.ticks != second.ticks;

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
        /// Reads the timestamp and prevents the processor from reordering memory operations.
        /// </summary>
        /// <param name="location">The managed pointer to the timestamp.</param>
        /// <returns>The value at the specified location.</returns>
        public static Timestamp VolatileRead(ref Timestamp location)
        {
            Debug.Assert(Unsafe.SizeOf<Timestamp>() == sizeof(long));
            return new Timestamp(Volatile.Read(ref Unsafe.As<Timestamp, long>(ref location)));
        }

        /// <summary>
        /// Writes the timestamp and prevents the proces from reordering memory operations.
        /// </summary>
        /// <param name="location">The managed pointer to the timestamp.</param>
        /// <param name="newValue">The value to write.</param>
        public static void VolatileWrite(ref Timestamp location, Timestamp newValue)
        {
            Debug.Assert(Unsafe.SizeOf<Timestamp>() == sizeof(long));
            Volatile.Write(ref Unsafe.As<Timestamp, long>(ref location), newValue.ticks);
        }
    }
}