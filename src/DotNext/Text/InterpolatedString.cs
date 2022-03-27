using System.Runtime.CompilerServices;

namespace DotNext.Text;

using Buffers;

/// <summary>
/// Provides factory methods to create interpolated strings efficiently.
/// </summary>
public static class InterpolatedString
{
    /// <summary>
    /// Formats interpolated string as a rented buffer of characters.
    /// </summary>
    /// <param name="allocator">The allocator of the interpolated string.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>A buffer containing formatted string.</returns>
    public static MemoryOwner<char> Allocate(MemoryAllocator<char>? allocator, IFormatProvider? provider, [InterpolatedStringHandlerArgument("allocator", "provider")] ref PoolingInterpolatedStringHandler handler)
        => handler.DetachBuffer();

    /// <summary>
    /// Formats interpolated string as a rented buffer of characters.
    /// </summary>
    /// <param name="allocator">The allocator of the interpolated string.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>A buffer containing formatted string.</returns>
    public static MemoryOwner<char> Allocate(MemoryAllocator<char>? allocator, [InterpolatedStringHandlerArgument("allocator")] ref PoolingInterpolatedStringHandler handler)
        => Allocate(allocator, null, ref handler);
}