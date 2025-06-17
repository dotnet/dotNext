using System.Buffers;

namespace DotNext.Buffers;

using static Runtime.Intrinsics;

public sealed class UnmanagedMemoryPoolTests : Test
{
    [Fact]
    public static void ReadWriteTest()
    {
        using var owner = UnmanagedMemory.Allocate<ushort>(3);
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
    public static unsafe void ArrayInteropTest()
    {
        using var owner = UnmanagedMemory.Allocate<ushort>(3);
        Span<ushort> array = owner.Span;
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
    public static unsafe void UnmanagedMemoryAllocation()
    {
        using var owner = UnmanagedMemory.GetAllocator<ushort>(false).Invoke(3);
        Span<ushort> array = owner.Span;
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;

        var dest = new ushort[array.Length];
        array.CopyTo(dest);
        Equal(10, dest[0]);
        Equal(20, dest[1]);
        Equal(30, dest[2]);
    }

    [Fact]
    public static void ResizeTest()
    {
        using var owner = UnmanagedMemory.Allocate<long>(5);
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
        using var owner = UnmanagedMemory.Allocate<long>(5);
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
        owner.Span.CopyTo(array, out _);
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
        using var manager = UnmanagedMemory.AllocateZeroed<long>(2);
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

    [Fact]
    public static void Pooling()
    {
        using var pool = new UnmanagedMemoryPool<long>(10) { TrackAllocations = true, AllocateZeroedMemory = true };
        using var manager = pool.Rent(2);
        Equal(2, manager.Memory.Length);

        Equal(0, manager.Memory.Span[0]);
        Equal(0, manager.Memory.Span[1]);
        manager.Memory.Span[0] = 10L;
        manager.Memory.Span[1] = 20L;
        Equal(10L, manager.Memory.Span[0]);
        Equal(20L, manager.Memory.Span[1]);
    }

    [Fact]
    public static void EnumeratorTest()
    {
        using var owner = UnmanagedMemory.Allocate<int>(3);
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
    public static unsafe void ZeroMem()
    {
        using var memory = UnmanagedMemory.Allocate<byte>(3);
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
        using var memory = UnmanagedMemory.Allocate<ushort>(3);
        using var ms = new MemoryStream();
        new ushort[] { 1, 2, 3 }.AsSpan().CopyTo(memory.Span);
        ms.Write(memory.Bytes);
        Equal(6L, ms.Length);
        True(ms.TryGetBuffer(out var buffer));
        buffer.AsSpan().ForEach((ref byte value, int _) =>
        {
            if (value is 1)
                value = 20;
        });

        ms.Position = 0;
        Equal(6, ms.Read(memory.Bytes));
        Equal(20, memory.Span[0]);
    }

    [Fact]
    public static unsafe void ToStreamConversion()
    {
        using var memory = UnmanagedMemory.AllocateZeroed<byte>(3);
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
        using var memory = UnmanagedMemory.AllocateZeroed<int>(3) as MemoryManager<int>;
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
        int* ptr = stackalloc int[] { 10, 20, 30 };
        var memory = UnmanagedMemory.AsMemory(ptr, 3);
        False(memory.IsEmpty);
        Equal([10, 20, 30], memory.Span);

        KeepAlive(in memory);
    }

    [Fact]
    public static unsafe void AllocateSystemPages()
    {
        using var owner = UnmanagedMemory.AllocateSystemPages(2);
        using var handle = owner.Memory.Pin();
        True((nuint)handle.Pointer % (uint)Environment.SystemPageSize is 0);
    }

    [Fact]
    public static void AllocateSystemPagesInvalidPageCount()
    {
        Throws<ArgumentOutOfRangeException>(static () => UnmanagedMemory.AllocateSystemPages(-1));
        
        var owner = UnmanagedMemory.AllocateSystemPages(0);
        Equal(Memory<byte>.Empty, owner.Memory);
    }

    [Fact]
    public static unsafe void AllocatePageAlignedMemory()
    {
        using var owner = UnmanagedMemory.AllocatePageAlignedMemory(Environment.SystemPageSize - 1, roundUpSize: true);
        Equal(Environment.SystemPageSize, owner.Memory.Length);
        
        using var handle = owner.Memory.Pin();
        True((nuint)handle.Pointer % (uint)Environment.SystemPageSize is 0);
    }

    [Fact]
    public static void AllocatePageAlignedMemoryInvalidSize()
    {
        Throws<ArgumentOutOfRangeException>(static () => UnmanagedMemory.AllocatePageAlignedMemory(-1));

        var owner = UnmanagedMemory.AllocatePageAlignedMemory(0);
        Equal(Memory<byte>.Empty, owner.Memory);
    }

    [Fact]
    public static void DiscardPages()
    {
        using var systemPages = UnmanagedMemory.AllocateSystemPages(1);
        systemPages.Memory.Span[0] = 42;
        
        UnmanagedMemory.Discard(systemPages.Memory.Span);
        Equal(0, systemPages.Memory.Span[0]);
    }
}