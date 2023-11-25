using System.ComponentModel;

namespace DotNext.Buffers;

public static partial class BufferHelpers
{
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