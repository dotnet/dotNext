using System;
using Xunit;

namespace DotNext.Threading
{
    public sealed class VolatileContainerTests : Assert
    {
        [Fact]
        public static void ReadWrite()
        {
            var container = new VolatileContainer<Guid>();
            var value = Guid.NewGuid();
            container.Value = value;
            Equal(value, container.Value);
        }

        [Fact]
        public static void AtomicExchange()
        {
            var container = new VolatileContainer<Guid>();
            Equal(Guid.Empty, container.Value);

            container.Exchange(Guid.NewGuid(), out var value);
            Equal(Guid.Empty, value);
            NotEqual(Guid.Empty, container.Value);
        }

        [Fact]
        public static void AtomicUpdate()
        {
            var container = new VolatileContainer<Guid>();
            container.GetAndUpdate((in Guid current, out Guid newValue) => newValue = Guid.NewGuid(), out var value);
            Equal(Guid.Empty, value);
            NotEqual(Guid.Empty, container.Value);
        }

        [Fact]
        public static void CompareAndSet()
        {
            var container = new VolatileContainer<Guid>();
            True(container.CompareAndSet(Guid.Empty, Guid.NewGuid()));
            NotEqual(Guid.Empty, container.Value);
            False(container.CompareAndSet(Guid.Empty, Guid.NewGuid()));
            NotEqual(Guid.Empty, container.Value);
        }

        private static void Add(in decimal current, in decimal value, out decimal result) => result = current + value;

        [Fact]
        public static void Accumulation()
        {
            var container = new VolatileContainer<decimal>();
            container.GetAndAccumulate(10M, Add, out var value);
            Equal(decimal.Zero, value);
            Equal(10M, container.Value);
        }
    }
}