namespace DotNext.Buffers;

using Runtime.InteropServices;

/// <summary>
/// Represents unmanaged memory access that allows
/// to obtain <see cref="Memory{T}"/> pointing to the
/// unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of elements to store in memory.</typeparam>
public interface IUnmanagedMemoryOwner<T> : IUnmanagedMemory<T>, IUnmanagedArray<T>
    where T : unmanaged
{
    /// <summary>
    /// Resizes a block of memory represented by this instance.
    /// </summary>
    /// <remarks>
    /// This method is dangerous becase it invalidates all buffers returned by <see cref="System.Buffers.IMemoryOwner{T}.Memory"/> property.
    /// </remarks>
    /// <param name="length">The new number of elements in the unmanaged array.</param>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
    void Reallocate(int length);
}