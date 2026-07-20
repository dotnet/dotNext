namespace DotNext.Threading;

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
    public static long Read(ref readonly long location)
        => IsAtomic<long>() ? Volatile.Read(in location) : Interlocked.Read(in location);

    /// <summary>
    /// Writes atomically the value at the specified location in the memory.
    /// </summary>
    /// <remarks>
    /// This method works correctly on 32-bit and 64-bit architectures.
    /// </remarks>
    /// <param name="location">The location of the value.</param>
    /// <param name="value">The desired value at the specified location.</param>
    public static void Write(ref long location, long value)
    {
        if (IsAtomic<long>())
        {
            Volatile.Write(ref location, value);
        }
        else
        {
            Interlocked.Exchange(ref location, value);
        }
    }
}