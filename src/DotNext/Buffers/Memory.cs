using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents methods to work with memory pools and buffers.
/// </summary>
public static partial class Memory
{
    /// <summary>
    /// Extends <see cref="ReadOnlyMemory{T}"/> type.
    /// </summary>
    /// <param name="memory">A contiguous region of arbitrary memory.</param>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    extension<T>(ReadOnlyMemory<T> memory)
    {
        /// <summary>
        /// Trims the memory block to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original block returned.
        /// </summary>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed memory block.</returns>
        public ReadOnlyMemory<T> TrimLength(int maxLength)
            => memory.Length <= maxLength ? memory : memory.Slice(0, maxLength);

        /// <summary>
        /// Trims the memory block to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original block returned.
        /// </summary>
        /// <param name="x">The memory to trim.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed memory block.</returns>
        public static ReadOnlyMemory<T> operator %(ReadOnlyMemory<T> x, int maxLength)
            => x.TrimLength(maxLength);
    }
    
    /// <summary>
    /// Extends <see cref="Memory{T}"/> type.
    /// </summary>
    /// <param name="memory">A contiguous region of arbitrary memory.</param>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    extension<T>(Memory<T> memory)
    {
        /// <summary>
        /// Trims the memory block to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original block returned.
        /// </summary>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed memory block.</returns>
        public Memory<T> TrimLength(int maxLength)
            => memory.Length <= maxLength ? memory : memory.Slice(0, maxLength);

        /// <summary>
        /// Trims the memory block to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original block returned.
        /// </summary>
        /// <param name="x">The memory to trim.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed memory block.</returns>
        public static Memory<T> operator %(Memory<T> x, int maxLength)
            => x.TrimLength(maxLength);
    }

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
    /// <param name="owner">The buffer owner to resize.</param>
    /// <param name="newLength">A new length of the buffer.</param>
    /// <param name="allocator">The allocator to be called if the requested length is larger than the requested length.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than zero.</exception>
    public static void Resize<T>(this ref MemoryOwner<T> owner, int newLength, MemoryAllocator<T> allocator)
    {
        if (!owner.TryResize(newLength))
        {
            var newBuffer = allocator.AllocateAtLeast(newLength);
            owner.Span.CopyTo(newBuffer.Span);
            owner.Dispose();
            owner = newBuffer;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Write<T, TWriter>(TWriter writer, ReadOnlySpan<T> value)
        where TWriter : struct, IBufferWriter<T>, allows ref struct
    {
        var destination = GetSpan(writer);

        // Fast path, try copying to the available memory directly
        if (value.Length <= destination.Length)
        {
            value.CopyTo(destination);
            writer.Advance(value.Length);
        }
        else
        {
            WriteMultiSegment(writer, value, destination);
        }

        static void WriteMultiSegment(TWriter writer, ReadOnlySpan<T> source, Span<T> destination)
        {
            for (;; destination = GetSpan(writer))
            {
                var writtenCount = source >>> destination;
                writer.Advance(writtenCount);
                source = source.Slice(writtenCount);
                if (source.IsEmpty)
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Span<T> GetSpan(TWriter writer) => typeof(TWriter) == typeof(BufferWriterSlim<T>.Ref)
            ? Unsafe.As<TWriter, BufferWriterSlim<T>.Ref>(ref writer).Value.InternalGetSpan(0)
            : writer.GetSpan();
    }
}