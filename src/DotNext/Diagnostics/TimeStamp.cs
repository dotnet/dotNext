using System;
using static System.Diagnostics.Stopwatch;

namespace DotNext.Diagnostics
{
    /// <summary>
    /// Represents time stamp.
    /// </summary>
    /// <remarks>
    /// This class can be used as allocation-free alternative to <see cref="System.Diagnostics.Stopwatch"/>.
    /// </remarks>
    public readonly struct TimeStamp : IEquatable<TimeStamp>
    {
        private readonly long ticks;

        private TimeStamp(long ticks) => this.ticks = ticks;

        /// <summary>
        /// Gets current point in time.
        /// </summary>
        public static TimeStamp Current => new TimeStamp(GetTimestamp());

        private static long ToTicks(double duration)
            => (long)(TimeSpan.TicksPerSecond * duration / Frequency);

        /// <summary>
        /// Gets <see cref="TimeSpan"/> representing this time stamp.
        /// </summary>
        public TimeSpan Value => new TimeSpan(ToTicks(ticks));

        /// <summary>
        /// Gets precise difference between the current point in time and this time stamp.
        /// </summary>
        public TimeSpan Elapsed => new TimeSpan(ToTicks(Math.Max(0L, GetTimestamp() - ticks)));

        /// <summary>
        /// Gets <see cref="TimeSpan"/> representing the given time stamp.
        /// </summary>
        /// <param name="stamp">The time stamp to convert.</param>
        public static implicit operator TimeSpan(TimeStamp stamp) => stamp.Value;

        /// <summary>
        /// Determines whether the current time stamp equals to the specified time stamp.
        /// </summary>
        /// <param name="other">The time stamp to compare.</param>
        /// <returns><see langword="true"/> if this time stamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(TimeStamp other) => ticks == other.ticks;

        /// <summary>
        /// Determines whether the current time stamp equals to the specified time stamp.
        /// </summary>
        /// <param name="other">The time stamp to compare.</param>
        /// <returns><see langword="true"/> if this time stamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is TimeStamp stamp && Equals(stamp);

        /// <summary>
        /// Computes hash code for this time stamp.
        /// </summary>
        /// <returns>The hash code of this time stamp.</returns>
        public override int GetHashCode() => ticks.GetHashCode();

        /// <summary>
        /// Gets time stamp in the form of the string.
        /// </summary>
        /// <returns>The string representing this time stamp.</returns>
        public override string ToString() => Value.ToString();

        /// <summary>
        /// Determines whether the two time stamps are equal.
        /// </summary>
        /// <param name="first">The first time stamp to compare.</param>
        /// <param name="second">The second time stamp to compare.</param>
        /// <returns><see langword="true"/> if both time stamps are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(TimeStamp first, TimeStamp second) => first.ticks == second.ticks;

        /// <summary>
        /// Determines whether the two time stamps are equal.
        /// </summary>
        /// <param name="first">The first time stamp to compare.</param>
        /// <param name="second">The second time stamp to compare.</param>
        /// <returns><see langword="false"/> if both time stamps are equal; otherwise, <see langword="true"/>.</returns>
        public static bool operator !=(TimeStamp first, TimeStamp second) => first.ticks != second.ticks; 
    }
}