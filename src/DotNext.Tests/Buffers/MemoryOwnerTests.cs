using System.Buffers;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Buffers;

public sealed class MemoryOwnerTests : Test
{
    [Fact]
    public static void RentFromArrayPool()
    {
        using var owner = new MemoryOwner<byte>(ArrayPool<byte>.Shared, 10);
        Equal(10, owner.Memory.Length);
        Equal(owner.Length, owner.Memory.Length);
    }

    [Fact]
    public static void DefaultValue()
    {
        using var owner = new MemoryOwner<decimal>();
        True(owner.IsEmpty);
        True(owner.Memory.IsEmpty);
    }

    [Fact]
    public static void RentFromMemoryPool()
    {
        using var owner = MemoryPool<byte>.Shared.ToAllocator().Invoke(10);
        Equal(10, owner.Memory.Length);
        owner[1] = 42;
        Equal(42, owner[1]);
    }

    [Fact]
    public static void RentFromMemoryPool2()
    {
        using var owner = new MemoryOwner<byte>(MemoryPool<byte>.Shared.Rent, 10);
        Equal(10, owner.Memory.Length);
        owner[1] = 42;
        Equal(42, owner[1]);
    }

    [Fact]
    public static void RentFromMemoryPool3()
    {
        Func<int, IMemoryOwner<byte>> provider = MemoryPool<byte>.Shared.Rent;
        using var owner = provider.ToAllocator().Invoke(10);
        Equal(10, owner.Memory.Length);
        owner[1] = 42;
        Equal(42, owner.Span[1]);
    }

    [Fact]
    public static void RentFromMemoryPoolDefaultSize()
    {
        using var owner = new MemoryOwner<byte>(MemoryPool<byte>.Shared);
        False(owner.Memory.IsEmpty);
    }

    [Fact]
    public static void WrapArray()
    {
        var array = new byte[50];
        using var owner = new MemoryOwner<byte>(array, length: 42);
        Equal(42, owner.Memory.Length);
        owner[2] = 10;
        Equal(10, array[2]);

        Throws<ArgumentOutOfRangeException>(() => new MemoryOwner<byte>(array, length: array.Length + 1));
    }

    public static TheoryData<MemoryAllocator<int>> ExactSizeAllocators =>
    [
        MemoryAllocator<int>.Array,
        MemoryAllocator<int>.Pinned,
        MemoryAllocator<int>.Unmanaged,
        MemoryAllocator<int>.UnmanagedZeroMem
    ];

    [Theory]
    [MemberData(nameof(ExactSizeAllocators))]
    public static void ArrayAllocation(MemoryAllocator<int> allocator)
    {
        using var owner = allocator(4);
        Equal(4, owner.Length);
    }

    [Fact]
    public static void ArrayAllocatorCache()
    {
        Same(MemoryAllocator<byte>.Array, MemoryAllocator<byte>.Array);
        Same(MemoryAllocator<byte>.Pinned, MemoryAllocator<byte>.Pinned);
        Same(MemoryAllocator<byte>.Default, MemoryAllocator<byte>.Default);
        Same(MemoryAllocator<byte>.ArrayPool, MemoryAllocator<byte>.ArrayPool);
        Same(MemoryAllocator<byte>.ByteArrayPool, MemoryAllocator<byte>.ByteArrayPool);
    }

    [Fact]
    public static void RawReference()
    {
        var owner = new MemoryOwner<byte>([]);
        True(Unsafe.IsNullRef(ref owner.DataRef));

        owner = default;
        True(Unsafe.IsNullRef(ref owner.DataRef));

        owner = new([10]);
        Equal(10, owner.DataRef);
    }

    [Fact]
    public static void SetBufferLength()
    {
        var buffer = default(MemoryOwner<byte>);
        True(buffer.TryResize(0));
        False(buffer.TryResize(10));
        Throws<ArgumentOutOfRangeException>(() => buffer.TryResize(-1));

        buffer = new MemoryOwner<byte>([10, 20, 30]);
        True(buffer.TryResize(1));
        True(buffer.TryResize(3));
        False(buffer.TryResize(10));
    }

    [Fact]
    public static void ResizeBuffer()
    {
        var allocator = MemoryAllocator<byte>.Array;
        var buffer = default(MemoryOwner<byte>);

        buffer.Resize(10, allocator);
        Equal(10, buffer.Length);

        buffer.Resize(3, allocator);
        Equal(3, buffer.Length);

        True(buffer.TryResize(10));
    }

    [Fact]
    public static void FromFactoryWithSize()
    {
        const int size = 512;
        using var buffer = new MemoryOwner<byte>(MemoryPool<byte>.Shared.Rent, size);
        True(buffer.Length >= size);
    }
    
    [Fact]
    public static void FromFactoryWithoutSize()
    {
        const int size = 512;
        using var buffer = new MemoryOwner<byte>(static () => MemoryPool<byte>.Shared.Rent(size));
        True(buffer.Length >= size);
    }

    public static TheoryData<MemoryAllocator<char>, int> AllocatorsAndSizes
        => new(from allocator in ReadOnlyCollection.CreateCollection(
                MemoryAllocator<char>.Default,
                MemoryAllocator<char>.ArrayPool,
                MemoryAllocator<char>.ByteArrayPool,
                MemoryAllocator<char>.Array,
                MemoryAllocator<char>.Pinned,
                MemoryAllocator<char>.Unmanaged,
                MemoryAllocator<char>.UnmanagedZeroMem)
            from bufferSize in ReadOnlyCollection.CreateCollection(1, 2, 3, 4, 10, 16, 32, 64, 1024)
            select (allocator, bufferSize));

    [Theory]
    [MemberData(nameof(AllocatorsAndSizes))]
    public static unsafe void AllocateRelease(MemoryAllocator<char> allocator, int bufferSize)
    {
        using var owner = allocator.Invoke(bufferSize);
        True(owner.Length >= bufferSize);

        var span = owner.Memory.Span;
        using (var handle = owner.Memory.Pin())
        {
            Unsafe.AsRef<char>(handle.Pointer) = 'b';
            Equal('b', span[0]);
        }
        
        span[0] = 'a';
        Equal('a', span[0]);
    }

    [Fact]
    public static void UseByteArrayPoolForGuid()
    {
        using var owner = MemoryAllocator<Guid>.ByteArrayPool.Invoke(64);
        True(MemoryMarshal.TryGetMemoryManager<Guid, MemoryManager<Guid>>(owner.Memory, out _));
    }
}