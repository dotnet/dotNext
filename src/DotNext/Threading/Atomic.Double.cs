using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Diagnostics.CodeAnalysis;
using Runtime.CompilerServices;

public static partial class Atomic
{
    /// <summary>
    /// Reads atomically the value from the specified location in the memory.
    /// </summary>
    /// <remarks>
    /// This method works correctly on 32-bit and 64-bit architectures.
    /// </remarks>
    /// <param name="location">The location of the value.</param>
    /// <returns>The value at the specified location.</returns>
    [CLSCompliant(false)]
    [MemoryOrdering(MemoryOrdering.Acquire)]
    [Obsolete("Use Volatile.Read instead.")]
    public static double Read(ref readonly double location)
        => Volatile.Read(in location);

    /// <summary>
    /// Writes atomically the value at the specified location in the memory.
    /// </summary>
    /// <remarks>
    /// This method works correctly on 32-bit and 64-bit architectures.
    /// </remarks>
    /// <param name="location">The location of the value.</param>
    /// <param name="value">The desired value at the specified location.</param>
    [CLSCompliant(false)]
    [MemoryOrdering(MemoryOrdering.Release)]
    [Obsolete("Use Volatile.Write instead.")]
    public static void Write(ref double location, double value)
        => Volatile.Write(ref location, value);
}