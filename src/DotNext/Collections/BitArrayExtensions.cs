using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections;

using Runtime.InteropServices;

/// <summary>
/// Extends <see cref="BitArray"/> type.
/// </summary>
public static class BitArrayExtensions
{
    /// <summary>
    /// Extends <see cref="BitArray"/> type.
    /// </summary>
    /// <param name="array">An array of bits.</param>
    extension(BitArray array)
    {
        /// <summary>
        /// Gets the number of bits set to 1.
        /// </summary>
        public long PopCount
        {
            get
            {
                var result = 0L;
                Span<byte> bytes;
                for (bytes = CollectionsMarshal.AsBytes(array);
                     bytes.Length >= nuint.Size;
                     bytes = bytes.Slice(nuint.Size))
                {
                    result += BitOperations.PopCount(
                        Unsafe.ReadUnaligned<nuint>(in MemoryMarshal.GetReference(bytes)));
                }

                foreach (var value in bytes)
                {
                    result += BitOperations.PopCount(value);
                }

                return result;
            }
        }

        /// <summary>
        /// Creates an array of bits from the blittable value.
        /// </summary>
        /// <param name="value">The value to convert to the bits.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <returns><paramref name="value"/> represented as an array of bits.</returns>
        public static BitArray Create<T>(in T value)
            where T : unmanaged, allows ref struct
            => Create(MemoryMarshal.AsReadOnlyBytes(in value));

        /// <summary>
        /// Creates an array of bits from the bytes.
        /// </summary>
        /// <param name="source">The sequence of bytes.</param>
        /// <returns>An array of bits reconstructed from the array of bytes.</returns>
        public static BitArray Create(ReadOnlySpan<byte> source)
        {
            var result = new BitArray(length: source.Length * 8);
            source.CopyTo(CollectionsMarshal.AsBytes(result));
            return result;
        }
    }
}