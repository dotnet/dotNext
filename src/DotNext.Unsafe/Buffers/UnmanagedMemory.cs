using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Numerics;
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

    /// <summary>
    /// Wraps unmanaged pointer to <see cref="Memory{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    /// <param name="pointer">The pointer to a sequence of elements.</param>
    /// <param name="length">The number of elements.</param>
    /// <returns></returns>
    [CLSCompliant(false)]
    public static unsafe Memory<T> AsMemory<T>(T* pointer, int length)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(pointer);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        return length > 0
            ? new UnmanagedMemory<T>(pointer, length).Memory
            : Memory<T>.Empty;
    }

    /// <summary>
    /// Allocates a specified number of system pages.
    /// </summary>
    /// <param name="pageCount">The number of system pages to be allocated.</param>
    /// <returns>A memory owner that represents allocated system pages.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageCount"/> is negative or zero.</exception>
    public static IMemoryOwner<byte> AllocateSystemPages(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);

        return pageCount is 0 ? UnmanagedMemory<byte>.Empty() : new SystemPageManager(pageCount);
    }

    /// <summary>
    /// Allocates page-aligned memory.
    /// </summary>
    /// <param name="size">The number of bytes to be allocated.</param>
    /// <param name="roundUpSize"><see langword="true"/> to round up the <paramref name="size"/> to the page size; otherwise, <see langword="false"/>.</param>
    /// <returns>A memory owner that represents page-aligned memory block.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative or zero.</exception>
    [CLSCompliant(false)]
    public static IMemoryOwner<byte> AllocatePageAlignedMemory(int size, bool roundUpSize = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        return size is 0 ? UnmanagedMemory<byte>.Empty() : new SystemPageManager(size, roundUpSize);
    }
}

internal unsafe class UnmanagedMemory<T> : MemoryManager<T>
    where T : unmanaged
{
    private protected void* address;

    private UnmanagedMemory()
    {
    }
    
    internal UnmanagedMemory(void* address, int length)
    {
        Debug.Assert(address is not null);

        this.address = address;
        Length = length;
    }

    internal UnmanagedMemory(nint address, int length)
        : this(address.ToPointer(), length)
    {
    }

    internal static UnmanagedMemory<T> Empty() => new();

    public int Length { get; private protected set; }

    public ref T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));

            return ref Unsafe.Add(ref Unsafe.AsRef<T>(address), index);
        }
    }

    public sealed override Span<T> GetSpan()
        => address is not null ? new(address, Length) : [];

    public sealed override Memory<T> Memory => address is not null ? CreateMemory(Length) : Memory<T>.Empty;

    public sealed override MemoryHandle Pin(int elementIndex = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)elementIndex, (uint)Length, nameof(elementIndex));

        return new(Unsafe.Add<T>(address, elementIndex));
    }

    public sealed override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
        address = null;
        Length = 0;
    }
}

internal class UnmanagedMemoryOwner<T> : UnmanagedMemory<T>, IUnmanagedMemory<T>
    where T : unmanaged
{
    private protected unsafe UnmanagedMemoryOwner(int length, delegate*<nuint, nuint, void* > allocator)
        : base(allocator((nuint)length, (nuint)sizeof(T)), length)
    {
    }

    internal static unsafe UnmanagedMemoryOwner<T> Create(int length)
        => new(length, &NativeMemory.Alloc);

    internal static unsafe UnmanagedMemoryOwner<T> CreateZeroed(int length)
        => new(length, &NativeMemory.AllocZeroed);

    public unsafe Pointer<T> Pointer => new((T*)address);

    Span<T> IUnmanagedMemory<T>.Span => GetSpan();

    unsafe void IUnmanagedMemory<T>.Reallocate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var size = (uint)length * (nuint)(uint)sizeof(T);
        address = NativeMemory.Realloc(address, size);
        Length = length;
    }

    bool IUnmanagedMemory<T>.SupportsReallocation => true;

    protected override unsafe void Dispose(bool disposing)
    {
        if (address is not null)
        {
            NativeMemory.Free(address);
        }

        base.Dispose(disposing);
    }

    [SuppressMessage("Reliability", "CA2015", Justification = "The caller must hold the reference to the memory object.")]
    ~UnmanagedMemoryOwner() => Dispose(disposing: false);
}

file sealed unsafe class SystemPageManager : UnmanagedMemory<byte>
{
    internal SystemPageManager(int pageCount)
        : base(Allocate(pageCount, out var length), length)
    {
    }

    internal SystemPageManager(int sizeInBytes, bool roundUp)
        : base(Allocate(ref sizeInBytes, roundUp), sizeInBytes)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (address is not null)
        {
            NativeMemory.AlignedFree(address);
        }

        base.Dispose(disposing);
    }

    private static void* Allocate(int pageCount, out int length)
    {
        Debug.Assert(pageCount > 0);
        
        var pageSize = Environment.SystemPageSize;
        length = checked(pageSize * pageCount);
        return NativeMemory.AlignedAlloc((uint)length, (uint)pageSize);
    }

    private static void* Allocate(ref int sizeInBytes, bool roundUp)
    {
        var size = (uint)sizeInBytes;
        var pageSize = (uint)Environment.SystemPageSize;
        if (roundUp)
        {
            size = size.RoundUp(pageSize);
            sizeInBytes = checked((int)size);
        }

        return NativeMemory.AlignedAlloc(size, pageSize);
    }
    
    [SuppressMessage("Reliability", "CA2015", Justification = "The caller must hold the reference to the memory object.")]
    ~SystemPageManager() => Dispose(disposing: false);
}