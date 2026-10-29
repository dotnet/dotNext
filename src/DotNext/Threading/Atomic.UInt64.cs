namespace DotNext.Threading;

using Diagnostics.CodeAnalysis;

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
    public static ulong Read(ref readonly ulong location)
        => IsAtomic<ulong>() ? Volatile.Read(in location) : Interlocked.Read(in location);

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
    public static void Write(ref ulong location, ulong value)
    {
        if (IsAtomic<ulong>())
        {
            Volatile.Write(ref location, value);
        }
        else
        {
            Interlocked.Exchange(ref location, value);
        }
    }
}