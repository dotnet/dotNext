using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Text;

using Buffers;

partial class StringInterpolation
{
    /// <summary>
    /// Formats interpolated string as a rented buffer of characters.
    /// </summary>
    /// <param name="allocator">The allocator of the interpolated string.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>A buffer containing formatted string.</returns>
    public static MemoryOwner<char> Interpolate(MemoryAllocator<char>? allocator, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(allocator), nameof(provider))] ref PoolingInterpolatedStringHandler handler)
        => handler.DetachBuffer();

    /// <summary>
    /// Formats interpolated string as a rented buffer of characters.
    /// </summary>
    /// <param name="allocator">The allocator of the interpolated string.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>A buffer containing formatted string.</returns>
    public static MemoryOwner<char> Interpolate(MemoryAllocator<char>? allocator, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler)
        => Interpolate(allocator, null, ref handler);

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="provider">The formatting provider.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this IBufferWriter<char> writer, IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] in BufferWriterInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this IBufferWriter<char> writer,
        [InterpolatedStringHandlerArgument(nameof(writer))] in BufferWriterInterpolatedStringHandler handler)
        => Interpolate(writer, null, in handler);

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="provider">The formatting provider.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this ref BufferWriterSlim<char> writer, IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] scoped in BufferWriterSlimInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this ref BufferWriterSlim<char> writer,
        [InterpolatedStringHandlerArgument(nameof(writer))] scoped in BufferWriterSlimInterpolatedStringHandler handler)
        => Interpolate(ref writer, null, in handler);
}