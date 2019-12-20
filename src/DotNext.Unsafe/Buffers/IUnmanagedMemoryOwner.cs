using System.Buffers;

namespace DotNext.Buffers
{
    using Runtime.InteropServices;

    /// <summary>
    /// Represents unmanaged memory access that allows
    /// to obtain <see cref="System.Memory{T}"/> pointing to the
    /// unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of elements to store in memory.</typeparam>
    public interface IUnmanagedMemoryOwner<T> : IMemoryOwner<T>, IUnmanagedArray<T>
        where T : unmanaged
    {
    }
}
