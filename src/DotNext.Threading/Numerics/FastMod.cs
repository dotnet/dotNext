using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Numerics;

/// <summary>
/// Internal use only.
/// </summary>
/// <param name="divisor">The divisor.</param>
[StructLayout(LayoutKind.Auto)]
[EditorBrowsable(EditorBrowsableState.Never)]
[CLSCompliant(false)]
public readonly struct FastMod(uint divisor)
{
    private readonly ulong multiplier = Is64BitProcess ? ulong.MaxValue / divisor + 1UL : 0UL;

    private static bool Is64BitProcess => IntPtr.Size is sizeof(ulong);

    /// <summary>
    /// Gets remainder.
    /// </summary>
    /// <param name="dividend">The value.</param>
    /// <returns>The remainder.</returns>
    public uint GetRemainder(uint dividend)
        => Is64BitProcess ? GetRemainderFast(dividend) : dividend % divisor;

    // Daniel Lemire's fastmod algorithm: https://lemire.me/blog/2016/06/27/a-fast-alternative-to-the-modulo-reduction/
    private uint GetRemainderFast(uint value)
    {
        Debug.Assert(divisor <= int.MaxValue);

        var result = (uint)(((((multiplier * value) >> 32) + 1UL) * divisor) >> 32);
        Debug.Assert(result == value % divisor);

        return result;
    }
}