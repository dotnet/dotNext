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
    [CLSCompliant(false)]
    public static ulong Read(ref readonly ulong location)
        => Is32BitProcess ? Interlocked.Read(in location) : Volatile.Read(in location);

    /// <summary>
    /// Writes atomically the value at the specified location in the memory.
    /// </summary>
    /// <remarks>
    /// This method works correctly on 32-bit and 64-bit architectures.
    /// </remarks>
    /// <param name="location">The location of the value.</param>
    /// <param name="value">The desired value at the specified location.</param>
    [CLSCompliant(false)]
    public static void Write(ref ulong location, ulong value)
    {
        if (Is32BitProcess)
        {
            Interlocked.Exchange(ref location, value);
        }
        else
        {
            Volatile.Write(ref location, value);
        }
    }
}