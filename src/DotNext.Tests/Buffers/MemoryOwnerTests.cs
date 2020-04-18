using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class MemoryOwnerTests : Test
    {
        [Fact]
        public static void RentFromArrayPool()
        {
            using var owner = ArrayPool<byte>.Shared.ToAllocator().Invoke(10);
            Equal(10, owner.Memory.Length);
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
        }

        [Fact]
        public static void RentFromMemoryPoolDefaultSize()
        {
            using var owner = new MemoryOwner<byte>(MemoryPool<byte>.Shared);
            False(owner.Memory.IsEmpty);
        }

        [Fact]
        public static void UsingArrayRental()
        {
            using MemoryOwner<byte> owner = new ArrayRental<byte>(20);
            Equal(20, owner.Memory.Length);
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
    }
}