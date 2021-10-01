namespace DotNext.Buffers;

public static partial class BufferHelpers
{
    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
    /// <param name="owner">The buffer owner to resize.</param>
    /// <param name="newLength">A new length of the buffer.</param>
    /// <param name="exactSize">
    /// <see langword="true"/> to ask allocator to allocate exactly <paramref name="newLength"/>;
    /// <see langword="false"/> to allocate at least <paramref name="newLength"/>.
    /// </param>
    /// <param name="allocator">The allocator to be called if the requested length is larger than the requested length.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than zero.</exception>
    public static void Resize<T>(this ref MemoryOwner<T> owner, int newLength, bool exactSize = true, MemoryAllocator<T>? allocator = null)
    {
        if (!owner.TryResize(newLength))
        {
            var newBuffer = allocator.Invoke(newLength, exactSize);
            owner.Memory.CopyTo(newBuffer.Memory);
            owner.Dispose();
            owner = newBuffer;
        }
    }
}