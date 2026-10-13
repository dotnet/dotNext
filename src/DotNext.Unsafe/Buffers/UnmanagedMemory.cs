using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

using Runtime.InteropServices;

/// <summary>
/// Provides native memory allocation facilities.
/// </summary>
/// <see cref="Memory"/>
public static partial class UnmanagedMemory
{
    /// <summary>
    /// Extends <see cref="IUnmanagedMemory{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the unmanaged memory block.</typeparam>
    extension<T>(IUnmanagedMemory<T>) where T : unmanaged
    {
        /// <summary>
        /// Allocates a block of unmanaged memory of the specified size, in elements.
        /// </summary>
        /// <param name="length">The number of elements to be allocated in unmanaged memory.</param>
        /// <returns>The object representing allocated unmanaged memory.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
        [CLSCompliant(false)]
        public static IUnmanagedMemory<T> Allocate(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
            return Allocate<T, DraftAllocator<T>>(length);
        }

        /// <summary>
        /// Allocates and zeroes a block of unmanaged memory of the specified size, in elements.
        /// </summary>
        /// <param name="length">The number of elements to be allocated in unmanaged memory.</param>
        /// <returns>The object representing allocated unmanaged memory.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
        [CLSCompliant(false)]
        public static IUnmanagedMemory<T> AllocateZeroed(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
            return Allocate<T, ZeroedAllocator<T>>(length);
        }

        private static UnmanagedMemoryOwner<T, TAllocator> Allocate<TAllocator>(int length)
            where TAllocator : unmanaged, INativeMemoryAllocator<T>, allows ref struct
            => new(length);
    }

    /// <summary>
    /// Extends <see cref="MemoryAllocator{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory block.</typeparam>
    extension<T>(MemoryAllocator<T>) where T : unmanaged
    {
        /// <summary>
        /// Gets the unmanaged memory allocator.
        /// </summary>
        public static MemoryAllocator<T> Unmanaged => GetAllocator<T>(zeroMem: false);

        /// <summary>
        /// Gets the unmanaged memory allocator that can be used to obtain a block of unmanaged memory filled with zeroes.
        /// </summary>
        public static MemoryAllocator<T> UnmanagedZeroMem => GetAllocator<T>(zeroMem: true);
        
        private static MemoryAllocator<T> GetAllocator(bool zeroMem)
        {
            return zeroMem
                ? Allocate<ZeroedAllocator<T>>
                : Allocate<DraftAllocator<T>>;

            static MemoryOwner<T> Allocate<TAllocator>(int length)
                where TAllocator : unmanaged, INativeMemoryAllocator<T>, allows ref struct
                => new(Allocate<T, TAllocator>, length);
        }
    }
}

internal unsafe class UnmanagedMemory<T> : MemoryManager<T>
    where T : unmanaged
{
    private protected T* address;

    private UnmanagedMemory()
    {
    }
    
    internal UnmanagedMemory(T* address, int length)
    {
        Debug.Assert(address is not null);

        this.address = address;
        Length = length;
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

internal class UnmanagedMemoryOwner<T, TAllocator> : UnmanagedMemory<T>, IUnmanagedMemory<T>
    where T : unmanaged
    where TAllocator : struct, INativeMemoryAllocator<T>, allows ref struct
{
    public unsafe UnmanagedMemoryOwner(int length)
        : base(INativeMemoryAllocator<T>.Allocate<TAllocator>((uint)length), length)
    {
    }

    public unsafe Pointer<T> Pointer => new(address);

    Span<T> IUnmanagedMemory<T>.Span => GetSpan();

    unsafe void IUnmanagedMemory<T>.Reallocate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        address = INativeMemoryAllocator<T>.Realloc(address, (uint)length);
        Length = length;
    }

    bool IUnmanagedMemory<T>.SupportsReallocation => true;

    protected override unsafe void Dispose(bool disposing)
    {
        if (address is not null)
        {
            INativeMemoryAllocator<T>.Free(address);
        }

        base.Dispose(disposing);
    }

    [SuppressMessage("Reliability", "CA2015", Justification = "The caller must hold the reference to the memory object.")]
    ~UnmanagedMemoryOwner() => Dispose(disposing: false);
}