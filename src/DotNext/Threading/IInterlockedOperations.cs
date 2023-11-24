using System.Numerics;

namespace DotNext.Threading;

/// <summary>
/// Represents interlocked operations.
/// </summary>
/// <typeparam name="T">The type that supports interlocked operations.</typeparam>
public interface IInterlockedOperations<T>
    where T : IEqualityOperators<T, T, bool>
{
    /// <summary>
    /// Reads the value of the specified location. On systems that require it, inserts a
    /// memory barrier that prevents the processor from reordering memory operations
    /// as follows: If a read or write appears after this method in the code, the processor
    /// cannot move it before this method.
    /// </summary>
    /// <param name="location">The location of the value.</param>
    /// <returns> The value that was read.</returns>
    static abstract T VolatileRead(ref readonly T location);

    /// <summary>
    /// Writes the specified value to the specified location. On systems that require it,
    /// inserts a memory barrier that prevents the processor from reordering memory operations
    /// as follows: If a read or write appears before this method in the code, the processor
    /// cannot move it after this method.
    /// </summary>
    /// <param name="location">The location of the value.</param>
    /// <param name="value">The value to write.</param>
    static abstract void VolatileWrite(ref T location, T value);

    /// <summary>
    /// Compares two values for equality and, if they
    /// are equal, replaces the first value.
    /// </summary>
    /// <param name="location">The destination, whose value is compared with comparand and possibly replaced.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at <paramref name="location"/>.</param>
    /// <returns>The original value in <paramref name="location"/>.</returns>
    static abstract T CompareExchange(ref T location, T value, T comparand);
}