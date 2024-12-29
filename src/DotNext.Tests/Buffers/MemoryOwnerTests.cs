using System.Buffers;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Buffers;

public sealed class MemoryOwnerTests : Test
{
    [Fact]
    public static void RentFromArrayPool()
    {
        using var owner = ArrayPool<byte>.Shared.ToAllocator().Invoke(10);
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
        var array = new byte[42];
        using var owner = new MemoryOwner<byte>(array);
        Equal(42, owner.Memory.Length);
        owner[2] = 10;
        Equal(10, array[2]);
    }

    public static TheoryData<MemoryAllocator<int>> GetArrayAllocators() => new()
    {
        Memory.GetArrayAllocator<int>(),
        Memory.GetPinnedArrayAllocator<int>(),
    };

    [Theory]
    [MemberData(nameof(GetArrayAllocators))]
    public static void ArrayAllocation(MemoryAllocator<int> allocator)
    {
        using var owner = allocator(4);
        Equal(4, owner.Length);
    }

    [Fact]
    public static void ArrayAllocatorCache()
    {
        Same(Memory.GetArrayAllocator<byte>(), Memory.GetArrayAllocator<byte>());
    }

    [Fact]
    public static void RawReference()
    {
        var owner = new MemoryOwner<byte>(Array.Empty<byte>());
        True(Unsafe.IsNullRef(ref Memory.GetReference(in owner)));

        owner = default;
        True(Unsafe.IsNullRef(ref Memory.GetReference(in owner)));

        owner = new([10]);
        Equal(10, Memory.GetReference(in owner));
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
        var allocator = Memory.GetArrayAllocator<byte>();
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
}