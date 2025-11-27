using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents methods to work with memory pools and buffers.
/// </summary>
public static partial class Memory
{
    /// <summary>
    /// Releases all resources encapsulated by the container.
    /// </summary>
    /// <remarks>
    /// This method calls <see cref="IDisposable.Dispose"/> for each
    /// object in the rented block.
    /// </remarks>
    /// <typeparam name="T">The type of items in the rented memory.</typeparam>
    /// <param name="owner">The rented memory.</param>
    public static void ReleaseAll<T>(this ref MemoryOwner<T> owner)
        where T : IDisposable
    {
        foreach (ref var item in owner.Span)
        {
            item.Dispose();
            item = default!;
        }

        owner.Clear(clearBuffer: false);
        owner = default;
    }

    /// <summary>
    /// Trims the memory block to specified length if it exceeds it.
    /// If length is less that <paramref name="maxLength" /> then the original block returned.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="memory">A contiguous region of arbitrary memory.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed memory block.</returns>
    public static ReadOnlyMemory<T> TrimLength<T>(this ReadOnlyMemory<T> memory, int maxLength)
        => memory.Length <= maxLength ? memory : memory.Slice(0, maxLength);

    /// <summary>
    /// Trims the memory block to specified length if it exceeds it.
    /// If length is less that <paramref name="maxLength" /> then the original block returned.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="memory">A contiguous region of arbitrary memory.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed memory block.</returns>
    public static Memory<T> TrimLength<T>(this Memory<T> memory, int maxLength)
        => memory.Length <= maxLength ? memory : memory.Slice(0, maxLength);

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
    /// <param name="owner">The buffer owner to resize.</param>
    /// <param name="newLength">A new length of the buffer.</param>
    /// <param name="allocator">The allocator to be called if the requested length is larger than the requested length.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than zero.</exception>
    public static void Resize<T>(this ref MemoryOwner<T> owner, int newLength, MemoryAllocator<T>? allocator = null)
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
    public static void Write<T, TWriter>(TWriter writer, ReadOnlySpan<T> value)
        where TWriter : struct, IBufferWriter<T>, allows ref struct
    {
        var destination = writer.GetSpan();

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

        static void WriteMultiSegment(TWriter writer, in ReadOnlySpan<T> source, Span<T> destination)
        {
            for (var input = source;;)
            {
                input.CopyTo(destination, out var writtenCount);
                writer.Advance(writtenCount);
                input = input.Slice(writtenCount);
                if (input.Length > 0)
                {
                    destination = writer.GetSpan();
                    continue;
                }

                break;
            }
        }
    }
}