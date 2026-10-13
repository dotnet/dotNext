using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Text;

using Buffers;

partial class StringInterpolation
{
    /// <summary>
    /// Extends <see cref="string"/> with interpolation string helpers.
    /// </summary>
    extension(string)
    {
        /// <summary>
        /// Formats interpolated string as a rented buffer of characters.
        /// </summary>
        /// <param name="allocator">The allocator of the interpolated string.</param>
        /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
        /// <param name="handler">The interpolated string handler.</param>
        /// <returns>A buffer containing formatted string.</returns>
        public static MemoryOwner<char> Interpolate(MemoryAllocator<char>? allocator, IFormatProvider? provider,
            [InterpolatedStringHandlerArgument(nameof(allocator), nameof(provider))] ref PoolingInterpolatedStringHandler handler)
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
        /// Interpolates the string using the initial buffer.
        /// </summary>
        /// <param name="initialBuffer">The initial buffer of chars.</param>
        /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
        /// <param name="handler">Interpolation string handler.</param>
        /// <returns>The buffer owner containing the interpolated characters.</returns>
        public static SpanOwner<char> Interpolate(scoped Span<char> initialBuffer, IFormatProvider? provider,
            [InterpolatedStringHandlerArgument(nameof(provider), nameof(initialBuffer))]
            ref DefaultInterpolatedStringHandler handler)
            => handler.DetachBuffer();
    }

    private static SpanOwner<char> DetachBuffer(this ref DefaultInterpolatedStringHandler handler)
    {
        // DefaultInterpolatedStringHandler internally uses ArrayPool<char>.Shared on overflow.
        // This behavior matches to SpanOwner<T>, which requires that the array must be rented
        // from the shared pool.
        var buffer = Unsafe.BitCast<ReadOnlySpan<char>, Span<char>>(handler.Text);
        return GetRentedBuffer(ref handler) is { } rentedBuffer
            ? new(new ArraySegment<char>(rentedBuffer, 0, buffer.Length))
            : new(buffer);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_arrayToReturnToPool")]
        static extern ref readonly char[] GetRentedBuffer(ref DefaultInterpolatedStringHandler handler);
    }

    /// <summary>
    /// Extends <see cref="IBufferWriter{T}"/> with string interpolation methods.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    extension(IBufferWriter<char> writer)
    {
        /// <summary>
        /// Writes interpolated string to the buffer.
        /// </summary>
        /// <param name="provider">The formatting provider.</param>
        /// <param name="handler">The handler of the interpolated string.</param>
        /// <returns>The number of written characters.</returns>
        public int Interpolate(IFormatProvider? provider,
            [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] in BufferWriterInterpolatedStringHandler handler)
            => handler.WrittenCount;

        /// <summary>
        /// Writes interpolated string to the buffer.
        /// </summary>
        /// <param name="handler">The handler of the interpolated string.</param>
        /// <returns>The number of written characters.</returns>
        public int Interpolate([InterpolatedStringHandlerArgument(nameof(writer))] in BufferWriterInterpolatedStringHandler handler)
            => Interpolate(writer, null, in handler);
    }

    /// <summary>
    /// Extends <see cref="BufferWriterSlim{T}"/> with string interpolation methods.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    extension(ref BufferWriterSlim<char> writer)
    {
        /// <summary>
        /// Writes interpolated string to the buffer.
        /// </summary>
        /// <param name="provider">The formatting provider.</param>
        /// <param name="handler">The handler of the interpolated string.</param>
        /// <returns>The number of written characters.</returns>
        public int Interpolate(IFormatProvider? provider,
            [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] scoped in BufferWriterSlimInterpolatedStringHandler handler)
            => handler.WrittenCount;

        /// <summary>
        /// Writes interpolated string to the buffer.
        /// </summary>
        /// <param name="handler">The handler of the interpolated string.</param>
        /// <returns>The number of written characters.</returns>
        public int Interpolate([InterpolatedStringHandlerArgument(nameof(writer))] scoped in BufferWriterSlimInterpolatedStringHandler handler)
            => Interpolate(ref writer, null, in handler);
    }
}