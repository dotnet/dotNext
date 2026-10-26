using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Provides low-level extensions to work with the memory.
/// </summary>
public static class MemoryMarshalExtensions
{
    /// <summary>
    /// Extends <see cref="MemoryMarshal"/> type.
    /// </summary>
    extension(MemoryMarshal)
    {
        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<byte> AsBytes<T>(ref T value)
            where T : unmanaged, allows ref struct
            => MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), sizeof(T));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(ref readonly T value)
            where T : unmanaged, allows ref struct
            => AsBytes(ref Unsafe.AsRef(in value));

        /// <summary>
        /// Returns <see cref="ReadOnlySpan{T}"/> over <see cref="byte"/> for the specified string.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <returns><see cref="ReadOnlySpan{T}"/> over <see cref="byte"/> representing <paramref name="value"/>.</returns>
        public static ReadOnlySpan<byte> AsReadOnlyBytes(string? value)
        {
            ReadOnlySpan<char> chars = value;
            var sizeInBytes = checked(chars.Length * sizeof(char));
            ref readonly var ch = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(chars));
            return MemoryMarshal.CreateReadOnlySpan(in ch, sizeInBytes);
        }
        
        /// <summary>
        /// Gets enumerator over all elements in the memory.
        /// </summary>
        /// <param name="memory">The memory block to be converted.</param>
        /// <typeparam name="T">The type of elements in the memory.</typeparam>
        /// <returns>The enumerator over all elements in the memory.</returns>
        /// <seealso cref="MemoryMarshal.ToEnumerable{T}(ReadOnlyMemory{T})"/>
        public static IEnumerator<T> ToEnumerator<T>(ReadOnlyMemory<T> memory)
        {
            return memory.IsEmpty
                ? Enumerable.Empty<T>().GetEnumerator()
                : MemoryMarshal.TryGetArray(memory, out var segment)
                    ? segment.GetEnumerator()
                    : ToEnumeratorSlow(memory);

            static IEnumerator<T> ToEnumeratorSlow(ReadOnlyMemory<T> memory)
            {
                for (nint i = 0; i < memory.Length; i++)
                    yield return Unsafe.Add(ref MemoryMarshal.GetReference(memory.Span), i);
            }
        }
    }
}