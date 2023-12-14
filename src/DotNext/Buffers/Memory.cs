using System.ComponentModel;

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
        where T : notnull, IDisposable
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
    /// Gets managed pointer to the first element in the rented memory block.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory block.</typeparam>
    /// <param name="owner">The rented memory block.</param>
    /// <returns>A managed pointer to the first element; or <see cref="System.Runtime.CompilerServices.Unsafe.NullRef{T}"/> if memory block is empty.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static ref T GetReference<T>(in MemoryOwner<T> owner) => ref owner.First;

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
}