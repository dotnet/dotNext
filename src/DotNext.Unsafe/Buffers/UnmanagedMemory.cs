using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime.InteropServices;

/// <summary>
/// Provides native memory allocation facilities.
/// </summary>
/// <see cref="Memory"/>
public static class UnmanagedMemory
{
    /// <summary>
    /// Allocates a block of unmanaged memory of the specified size, in elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the unmanaged memory block.</typeparam>
    /// <param name="length">The number of elements to be allocated in unmanaged memory.</param>
    /// <returns>The object representing allocated unmanaged memory.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
    [CLSCompliant(false)]
    public static IUnmanagedMemory<T> Allocate<T>(int length)
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        return UnmanagedMemoryOwner<T>.Create(length);
    }

    /// <summary>
    /// Allocates and zeroes a block of unmanaged memory of the specified size, in elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the unmanaged memory block.</typeparam>
    /// <param name="length">The number of elements to be allocated in unmanaged memory.</param>
    /// <returns>The object representing allocated unmanaged memory.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
    [CLSCompliant(false)]
    public static IUnmanagedMemory<T> AllocateZeroed<T>(int length)
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        return UnmanagedMemoryOwner<T>.CreateZeroed(length);
    }

    /// <summary>
    /// Gets allocator of unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory block.</typeparam>
    /// <param name="zeroMem"><see langword="true"/> to set all bits in the memory to zero; otherwise, <see langword="false"/>.</param>
    /// <returns>The unmanaged memory allocator.</returns>
    public static MemoryAllocator<T> GetAllocator<T>(bool zeroMem)
        where T : unmanaged
    {
        return zeroMem ? AllocateZeroed : Allocate;

        static MemoryOwner<T> Allocate(int length)
            => new(UnmanagedMemoryOwner<T>.Create, length);

        static MemoryOwner<T> AllocateZeroed(int length)
            => new(UnmanagedMemoryOwner<T>.CreateZeroed, length);
    }
}

internal unsafe class UnmanagedMemory<T> : MemoryManager<T>
    where T : unmanaged
{
    private readonly bool owner;
    private void* address;

    internal UnmanagedMemory(nint address, int length)
    {
        Debug.Assert(address is not 0);

        this.address = (void*)address;
        Length = length;
    }

    private protected UnmanagedMemory(int length, delegate*<nuint, nuint, void* > allocator)
    {
        Debug.Assert(length > 0);
        Debug.Assert(allocator is not null);

        address = allocator((nuint)length, (nuint)sizeof(T));
        owner = true;
    }

    protected nuint Address => (nuint)address;

    public int Length { get; private set; }

    public ref T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));

            return ref Unsafe.Add(ref Unsafe.AsRef<T>(address), index);
        }
    }

    internal void Reallocate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ObjectDisposedException.ThrowIf(address is null, this);

        var size = (nuint)length * (nuint)sizeof(T);
        address = NativeMemory.Realloc(address, size);
        Length = length;
    }

    public sealed override Span<T> GetSpan()
        => address is not null ? new(address, Length) : [];

    public sealed override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(address is null, this);
        ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);

        return new(Unsafe.Add<T>(address, elementIndex));
    }

    public sealed override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (address is not null && owner)
        {
            NativeMemory.Free(address);
        }

        address = null;
        Length = 0;
    }
}

internal class UnmanagedMemoryOwner<T> : UnmanagedMemory<T>, IUnmanagedMemory<T>
    where T : unmanaged
{
    private protected unsafe UnmanagedMemoryOwner(int length, delegate*<nuint, nuint, void* > allocator)
        : base(length, allocator)
    {
    }

    internal static unsafe UnmanagedMemoryOwner<T> Create(int length)
        => new(length, &NativeMemory.Alloc);

    internal static unsafe UnmanagedMemoryOwner<T> CreateZeroed(int length)
        => new(length, &NativeMemory.AllocZeroed);

    public Pointer<T> Pointer => new(Address);

    Span<T> IUnmanagedMemory<T>.Span => GetSpan();

    void IUnmanagedMemory<T>.Reallocate(int length) => Reallocate(length);

    bool IUnmanagedMemory<T>.SupportsReallocation => true;
}