using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AtomicContainerTests : Assert
    {
        [Fact]
        public static void ReadWrite()
        {
            var container = new Atomic<Guid>();
            var value = Guid.NewGuid();
            container.Value = value;
            Equal(value, container.Value);
        }

        [Fact]
        public static void AtomicExchange()
        {
            var container = new Atomic<Guid>();
            Equal(Guid.Empty, container.Value);

            container.Exchange(Guid.NewGuid(), out var value);
            Equal(Guid.Empty, value);
            NotEqual(Guid.Empty, container.Value);
        }

        [Fact]
        public static void AtomicUpdate()
        {
            var container = new Atomic<Guid>();
            container.GetAndUpdate((ref Guid current) => current = Guid.NewGuid(), out var value);
            Equal(Guid.Empty, value);
            NotEqual(Guid.Empty, container.Value);
        }

        [Fact]
        public static void CompareAndSet()
        {
            var container = new Atomic<Guid>();
            True(container.CompareAndSet(Guid.Empty, Guid.NewGuid()));
            NotEqual(Guid.Empty, container.Value);
            False(container.CompareAndSet(Guid.Empty, Guid.NewGuid()));
            NotEqual(Guid.Empty, container.Value);
        }

        private static void Add(ref decimal current, in decimal value) => current += value;

        [Fact]
        public static void Accumulation()
        {
            var container = new Atomic<decimal>();
            container.GetAndAccumulate(10M, Add, out var value);
            Equal(decimal.Zero, value);
            Equal(10M, container.Value);
        }
    }
}