using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

/// <summary>
/// Provides native memory allocation facilities.
/// </summary>
/// <see cref="MemoryAllocator"/>
public static class UnmanagedMemoryAllocator
{
    /// <summary>
    /// Allocates unmanaged memory and returns an object
    /// that controls its lifetime.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the unmanaged memory block.</typeparam>
    /// <param name="length">The number of elements to be allocated in unmanaged memory.</param>
    /// <param name="zeroMem"><see langword="true"/> to set all bits in the memory to zero; otherwise, <see langword="false"/>.</param>
    /// <returns>The object representing allocated unmanaged memory.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
    public static IUnmanagedMemoryOwner<T> Allocate<T>(int length, bool zeroMem = true)
        where T : unmanaged
        => UnmanagedMemoryAllocator<T>.Allocate(length, zeroMem);

    /// <summary>
    /// Gets allocator of unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory block.</typeparam>
    /// <param name="zeroMem"><see langword="true"/> to set all bits in the memory to zero; otherwise, <see langword="false"/>.</param>
    /// <returns>The unmanaged memory allocator.</returns>
    public static MemoryAllocator<T> GetAllocator<T>(bool zeroMem)
        where T : unmanaged
        => zeroMem ? UnmanagedMemoryAllocator<T>.ZeroedAllocator : UnmanagedMemoryAllocator<T>.Allocator;
}

internal static class UnmanagedMemoryAllocator<T>
    where T : unmanaged
{
    internal static readonly MemoryAllocator<T> Allocator, ZeroedAllocator;

    static UnmanagedMemoryAllocator()
    {
        Allocator = AllocateRaw;
        ZeroedAllocator = AllocateZeroMem;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static unsafe MemoryOwner<T> AllocateZeroMem(int length)
            => MemoryOwner<T>.Create(&Allocate, length, true);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static unsafe MemoryOwner<T> AllocateRaw(int length)
            => MemoryOwner<T>.Create(&Allocate, length, false);
    }

    internal static IUnmanagedMemoryOwner<T> Allocate(int length, bool zeroMem = true)
        => length > 0 ? new UnmanagedMemoryOwner<T>(length, zeroMem, false) : throw new ArgumentOutOfRangeException(nameof(length));
}