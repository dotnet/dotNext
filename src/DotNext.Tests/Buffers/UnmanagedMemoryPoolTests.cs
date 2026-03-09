using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime.InteropServices;

public sealed class UnmanagedMemoryPoolTests : Test
{
    [Fact]
    public static void ReadWriteTest()
    {
        using var owner = IUnmanagedMemory<ushort>.Allocate(3);
        var array = owner.Span;
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;
        Equal([10, 20, 30], owner.Span.ToArray());
        Equal(3, array.Length);
        Equal(3, owner.Length);
        Equal(6U, owner.Size);
        Equal(10, array[0]);
        Equal(20, array[1]);
        Equal(30, array[2]);

        array.Clear();
        Equal(0, array[0]);
        Equal(0, array[1]);
        Equal(0, array[2]);
    }

    [Fact]
    public static void ArrayInteropTest()
    {
        using var owner = IUnmanagedMemory<ushort>.Allocate(3);
        var array = owner.Span;
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;

        var dest = new ushort[array.Length];
        array.CopyTo(dest);
        Equal(10, dest[0]);
        Equal(20, dest[1]);
        Equal(30, dest[2]);
        dest[0] = 100;
        dest.CopyTo(owner.Span);
        Equal(100, array[0]);
    }

    [Fact]
    public static void ResizeTest()
    {
        using var owner = IUnmanagedMemory<long>.Allocate(5);
        Span<long> array = owner.Span;
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;
        array[3] = 40;
        array[4] = 50;
        Equal(50, array[4]);
        True(owner.SupportsReallocation);
        owner.Reallocate(2);
        array = owner.Span;
        Equal(2, array.Length);
        Equal(10, array[0]);
        Equal(20, array[1]);
    }

    [Fact]
    public static void SliceTest()
    {
        using var owner = IUnmanagedMemory<long>.Allocate(5);
        Span<long> span = owner.Span;
        span[0] = 10;
        span[1] = 20;
        span[2] = 30;
        span[3] = 40;
        span[4] = 50;
        var slice = span.Slice(1, 2);
        Equal(2, slice.Length);
        Equal(20, slice[0]);
        Equal(30, slice[1]);
        slice = span.Slice(2);
        Equal(3, slice.Length);
        Equal(30, slice[0]);
        Equal(40, slice[1]);
        Equal(50, slice[2]);
        var array = new long[3];
        Equal(array.Length, owner.Span >>> array);
        Equal(10, array[0]);
        Equal(20, array[1]);
        Equal(30, array[2]);
        array[0] = long.MaxValue;
        array.CopyTo(owner.Span);
        Equal(long.MaxValue, span[0]);
        Equal(20, span[1]);
    }

    [Fact]
    public static void Allocation()
    {
        using var manager = IUnmanagedMemory<long>.AllocateZeroed(2);
        Equal(2, manager.Length);

        Equal(sizeof(long) * 2U, manager.Size);
        Equal(0, manager.Span[0]);
        Equal(0, manager.Span[1]);
        manager.Pointer[0] = 10L;
        manager.Pointer[1] = 20L;
        Equal(10L, manager.Span[0]);
        Equal(20L, manager.Span[1]);
        Equal(10L, manager.Memory.Span[0]);
        Equal(20L, manager.Memory.Span[1]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void Pooling(bool trackAllocations, bool zeroedMemory)
    {
        using var pool = new UnmanagedMemoryPool<long>(10)
        {
            TrackAllocations = trackAllocations,
            AllocateZeroedMemory = zeroedMemory,
        };
        
        using var manager = pool.Rent(2);
        Equal(2, manager.Memory.Length);

        manager.Memory.Span[0] = 10L;
        manager.Memory.Span[1] = 20L;
        Equal(10L, manager.Memory.Span[0]);
        Equal(20L, manager.Memory.Span[1]);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public static void CheckDefaultBufferSize(int defaultBufferSize)
    {
        using var pool = new UnmanagedMemoryPool<byte>(1024)
        {
            DefaultBufferSize = defaultBufferSize,
        };

        Equal(defaultBufferSize, pool.DefaultBufferSize);

        using var memory = pool.Rent();
        Equal(defaultBufferSize, memory.Memory.Length);
    }

    [Fact]
    public static void EnumeratorTest()
    {
        using var owner = IUnmanagedMemory<int>.Allocate(3);
        var array = owner.Span;
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;
        Collection(
            array.ToArray(),
            Equal(10),
            Equal(20),
            Equal(30));
    }

    [Fact]
    public static void ZeroMem()
    {
        using var memory = IUnmanagedMemory<byte>.Allocate(3);
        memory.Span[0] = 10;
        memory.Span[1] = 20;
        memory.Span[2] = 30;
        memory.Clear();
        foreach (ref var b in memory.Bytes)
            Equal(0, b);
        Equal(0, memory.Span[0]);
        Equal(0, memory.Span[1]);
        Equal(0, memory.Span[2]);
    }

    [Fact]
    public static void StreamInterop()
    {
        using var memory = IUnmanagedMemory<ushort>.Allocate(3);
        using var ms = new MemoryStream();
        new ushort[] { 1, 2, 3 }.AsSpan().CopyTo(memory.Span);
        ms.Write(memory.Bytes);
        Equal(6L, ms.Length);
        True(ms.TryGetBuffer(out var buffer));
        buffer.AsSpan().ForEach((element, _) =>
        {
            if (element.Value is 1)
                element.Value = 20;
        });

        ms.Position = 0;
        Equal(6, ms.Read(memory.Bytes));
        Equal(20, memory.Span[0]);
    }

    [Fact]
    public static void ToStreamConversion()
    {
        using var memory = IUnmanagedMemory<byte>.AllocateZeroed(3);
        new byte[] { 10, 20, 30 }.AsSpan().CopyTo(memory.Bytes);
        using var stream = memory.AsStream();
        var bytes = new byte[3];
        Equal(3, stream.Read(bytes, 0, 3));
        Equal(10, bytes[0]);
        Equal(20, bytes[1]);
        Equal(30, bytes[2]);
    }

    [Fact]
    public static unsafe void Pinning()
    {
        using var memory = IUnmanagedMemory<int>.AllocateZeroed(3) as MemoryManager<int>;
        NotNull(memory);
        memory.GetSpan()[0] = 10;
        memory.GetSpan()[1] = 20;
        memory.GetSpan()[2] = 30;
        var handle = memory.Pin();
        Equal(10, *(int*)handle.Pointer);
        memory.Unpin();
        handle.Dispose();
        handle = memory.Pin(1);
        Equal(20, *(int*)handle.Pointer);
        memory.Unpin();
        handle.Dispose();
        handle = memory.Pin(2);
        Equal(30, *(int*)handle.Pointer);
        memory.Unpin();
        handle.Dispose();
    }

    [Fact]
    public static unsafe void MarshalAsMemory()
    {
        var ptr = stackalloc int[] { 10, 20, 30 };
        var memory = Memory<int>.FromPointer(ptr, 3);
        False(memory.IsEmpty);
        Equal([10, 20, 30], memory.Span);

        GC.KeepAlive(in memory);
    }

    [Fact]
    public static unsafe void AllocateSystemPages()
    {
        using var owner = NativeMemory.AllocateSystemPages(2);
        using var handle = owner.Memory.Pin();
        True((nuint)handle.Pointer % (uint)Environment.SystemPageSize is 0);
    }

    [Fact]
    public static void AllocateSystemPagesInvalidPageCount()
    {
        Throws<ArgumentOutOfRangeException>(static () => NativeMemory.AllocateSystemPages(-1));
        
        var owner = NativeMemory.AllocateSystemPages(0);
        Equal(Memory<byte>.Empty, owner.Memory);
    }

    [Fact]
    public static unsafe void AllocatePageAlignedMemory()
    {
        using var owner = NativeMemory.AllocatePageAlignedMemory(Environment.SystemPageSize - 1, roundUpSize: true);
        Equal(Environment.SystemPageSize, owner.Memory.Length);
        
        using var handle = owner.Memory.Pin();
        True((nuint)handle.Pointer % (uint)Environment.SystemPageSize is 0);
    }

    [Fact]
    public static void AllocatePageAlignedMemoryInvalidSize()
    {
        Throws<ArgumentOutOfRangeException>(static () => NativeMemory.AllocatePageAlignedMemory(-1));

        var owner = NativeMemory.AllocatePageAlignedMemory(0);
        Equal(Memory<byte>.Empty, owner.Memory);
    }

    [PlatformSpecificFact(["linux", "windows"])]
    public static void DiscardPages()
    {
        using var systemPages = NativeMemory.AllocateSystemPages(1);
        systemPages.Memory.Span[0] = 42;
        
        // On FreeBSD and MacOS, the method doesn't clear the memory
        NativeMemory.Discard(systemPages.Memory.Span);
        
        Equal(0, systemPages.Memory.Span[0]);
    }
    
    [Fact]
    public static void DiscardPinnedMemory()
    {
        var bytes = GC.AllocateArray<byte>(Environment.SystemPageSize * 2, pinned: true);
        True(NativeMemory.GetPageAlignedOffset(bytes, out var offset));
        
        NativeMemory.Discard(bytes.AsSpan(offset, Environment.SystemPageSize));
    }

    [Fact]
    public static void UnmanagedMemoryMarshalling()
    {
        using var memory = IUnmanagedMemory<long>.Allocate(2);
        Equal(memory.Pointer.Address, UnmanagedMemoryMarshaller<long>.ConvertToUnmanaged(memory));
    }
}