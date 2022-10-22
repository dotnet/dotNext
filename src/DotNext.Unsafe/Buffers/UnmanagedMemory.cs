using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

internal unsafe class UnmanagedMemory<T> : MemoryManager<T>
    where T : unmanaged
{
    private readonly bool owner;
    private void* address;

    internal UnmanagedMemory(IntPtr address, int length)
    {
        this.address = address.ToPointer();
        Length = length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static long SizeOf(int length) => Math.BigMul(length, sizeof(T));

    private protected UnmanagedMemory(int length, bool zeroMem)
    {
        Debug.Assert(length > 0);

        address = zeroMem
            ? NativeMemory.AllocZeroed((nuint)length, (nuint)sizeof(T))
            : NativeMemory.Alloc((nuint)length, (nuint)sizeof(T));
        Length = length;
        owner = true;
    }

    private protected IntPtr Address
    {
        get
        {
            return address is not null ? new(address) : Throw();

            [DoesNotReturn]
            [StackTraceHidden]
            IntPtr Throw() => throw new ObjectDisposedException(GetType().Name);
        }
    }

    public long Size => SizeOf(Length);

    public int Length { get; private set; }

    internal void Reallocate(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (address is null)
            throw new ObjectDisposedException(GetType().Name);

        Length = length;
        var size = (nuint)SizeOf(length);
        address = NativeMemory.Realloc(address, size);
    }

    public sealed override Span<T> GetSpan()
        => address is not null ? new(address, Length) : Span<T>.Empty;

    public sealed override MemoryHandle Pin(int elementIndex = 0)
    {
        if (address is null)
            throw new ObjectDisposedException(GetType().Name);

        return new(Unsafe.Add<T>(address, elementIndex));
    }

    public sealed override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (address != null && owner)
        {
            NativeMemory.Free(address);
        }

        address = null;
        Length = 0;
    }
}
