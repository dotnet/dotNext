using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections;

using Generic;
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

        /// <summary>
        /// Gets an enumerator over indices of set bits.
        /// </summary>
        public SetBitsEnumerator SetBits => new(array);
    }

    /// <summary>
    /// Gets an enumerator over set bits.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct SetBitsEnumerator : IEnumerator<SetBitsEnumerator, int>, IEnumerable<int>
    {
        private const int BitsInByte = 8;
        private readonly BitArray array;
        private int position;

        internal SetBitsEnumerator(BitArray array)
            => this.array = array;
        
        /// <summary>
        /// Gets the current index.
        /// </summary>
        public int Current { get; private set; }

        /// <summary>
        /// Advances to the next set bit.
        /// </summary>
        /// <returns><see langword="true"/> if the next set bit is found; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            for (var bytes = CollectionsMarshal.AsBytes(array); position < array.Length;)
            {
                var byteIndex = position / BitsInByte;
                var bitOffset = position & (BitsInByte - 1);
                var iterationResult = bytes.Length - byteIndex >= nuint.Size
                    ? MoveNext(Unsafe.ReadUnaligned<nuint>(in bytes[byteIndex]), byteIndex, bitOffset)
                    : MoveNext(bytes[byteIndex], byteIndex, bitOffset);

                if (iterationResult)
                    return true;
            }

            return false;
        }

        private bool MoveNext<T>(T word, int byteIndex, int bitOffset)
            where T : unmanaged, IBinaryInteger<T>
        {
            // Mask allows to remove previously inspected bits
            word &= ~((T.One << bitOffset) - T.One);
            if (word == T.Zero)
            {
                var bitsInWord = BitsInByte * Unsafe.SizeOf<T>();
                position += bitsInWord - bitOffset;
                return false;
            }

            var index = int.CreateTruncating(T.TrailingZeroCount(word));
            position = (Current = index + byteIndex * BitsInByte) + 1;
            return true;
        }

        /// <summary>
        /// Gets an enumerator over indices of set bits.
        /// </summary>
        /// <returns>An enumerator over indices of set bits.</returns>
        public SetBitsEnumerator GetEnumerator() => new(array);
        
        private IEnumerator<int> GetClassicEnumerator()
            => IEnumerator<int>.Create(GetEnumerator());

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
            => GetClassicEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetClassicEnumerator();
    }
}