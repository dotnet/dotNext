using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

using Runtime.InteropServices;

/// <summary>
/// Provides various binary transformations.
/// </summary>
public static partial class BinaryTransformations
{
    private interface IUnaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T value);
    }

    private interface IBinaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T x, T y);
    }

    /// <summary>
    /// Reverse bytes in the specified value of blittable type.
    /// </summary>
    /// <typeparam name="T">Blittable type.</typeparam>
    /// <param name="value">The value which bytes should be reversed.</param>
    public static void Reverse<T>(ref T value)
        where T : unmanaged
        => MemoryMarshal.AsBytes(ref value).Reverse();

    /// <summary>
    /// Extends <see cref="IBinaryFormattable{TSelf}"/> types.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <typeparam name="T">The implementing type.</typeparam>
    extension<T>(T value) where T : IBinaryFormattable<T>
    {
        /// <summary>
        /// Attempts to restore the object from its binary representation.
        /// </summary>
        /// <param name="source">The input buffer.</param>
        /// <param name="result">The restored object.</param>
        /// <returns><see langword="true"/> if the parsing done successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(scoped ReadOnlySpan<byte> source, [NotNullWhen(true)] out T? result)
        {
            if (source.Length >= T.Size)
            {
                result = T.Parse(source);
                return true;
            }

            result = default;
            return false;
        }
        
        /// <summary>
        /// Formats object as a sequence of bytes.
        /// </summary>
        /// <param name="allocator">The memory allocator.</param>
        /// <returns>The buffer containing formatted value.</returns>
        public MemoryOwner<byte> Format(MemoryAllocator<byte>? allocator = null)
        {
            var result = allocator.DefaultIfNull.AllocateExactly(T.Size);
            value.Format(result.Span);
            return result;
        }
        
        /// <summary>
        /// Attempts to format object as a sequence of bytes.
        /// </summary>
        /// <param name="destination">The output buffer.</param>
        /// <returns><see langword="true"/> if the value converted successfully; otherwise, <see langword="false"/>.</returns>
        public bool TryFormat(Span<byte> destination)
        {
            if (destination.Length >= T.Size)
            {
                value.Format(destination);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Restores the object from its binary representation.
        /// </summary>
        /// <param name="source">The input buffer.</param>
        /// <returns>The restored object.</returns>
        public static T Parse(in ReadOnlySequence<byte> source)
        {
            var fastBuffer = source.FirstSpan;
            return fastBuffer.Length >= T.Size
                ? T.Parse(fastBuffer)
                : ParseSlow(in source);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static T ParseSlow(in ReadOnlySequence<byte> input)
            {
                using var buffer = (uint)T.Size <= (uint)SpanOwner<byte>.StackallocThreshold
                    ? stackalloc byte[T.Size]
                    : new SpanOwner<byte>(T.Size);

                var writtenCount = input >>> buffer.Span;
                return T.Parse(buffer.Span.Slice(0, writtenCount));
            }
        }
    }
}