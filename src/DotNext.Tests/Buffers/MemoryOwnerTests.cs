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

    [Fact]
    public static void ArrayAllocation()
    {
        using var owner = MemoryAllocator.GetArrayAllocator<int>().Invoke(4, false);
        Equal(4, owner.Length);
    }

    [Fact]
    public static void ArrayAllocatorCache()
    {
        Same(MemoryAllocator.GetArrayAllocator<byte>(), MemoryAllocator.GetArrayAllocator<byte>());
    }

    [Fact]
    public static void RawReference()
    {
        var owner = new MemoryOwner<byte>(Array.Empty<byte>());
        True(Unsafe.IsNullRef(ref BufferHelpers.GetReference(in owner)));

        owner = default;
        True(Unsafe.IsNullRef(ref BufferHelpers.GetReference(in owner)));

        owner = new(new byte[] { 10 });
        Equal(10, BufferHelpers.GetReference(in owner));
    }

    [Fact]
    public static void SetBufferLength()
    {
        var buffer = default(MemoryOwner<byte>);
        True(buffer.TryResize(0));
        False(buffer.TryResize(10));
        Throws<ArgumentOutOfRangeException>(() => buffer.TryResize(-1));

        buffer = new MemoryOwner<byte>(new byte[] { 10, 20, 30 });
        True(buffer.TryResize(1));
        True(buffer.TryResize(3));
        False(buffer.TryResize(10));
    }

    [Fact]
    public static void ResizeBuffer()
    {
        var allocator = MemoryAllocator.GetArrayAllocator<byte>();
        var buffer = default(MemoryOwner<byte>);

        buffer.Resize(10, false, allocator);
        Equal(10, buffer.Length);

        buffer.Resize(3, false, allocator);
        Equal(3, buffer.Length);

        True(buffer.TryResize(10));
    }
}