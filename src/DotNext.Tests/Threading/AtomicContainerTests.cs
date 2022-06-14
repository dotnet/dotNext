using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AtomicContainerTests : Test
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
            static void Update(ref decimal value) => value = 42M;

            var container = new Atomic<decimal>() { Value = 1M };
            container.GetAndUpdate(Update, out var value);
            Equal(1M, value);
            Equal(42M, container.Value);
            container.Value = 0M;
            container.UpdateAndGet(Update, out value);
            Equal(42M, value);
            Equal(42M, container.Value);
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
            value = 42M;
            container.Swap(ref value);
            Equal(10M, value);
            Equal(42M, container.Value);
            container.AccumulateAndGet(1M, Add, out value);
            Equal(43M, value);
            Equal(43M, container.Value);
        }

        [Fact]
        public static void Cloning()
        {
            ICloneable container = new Atomic<decimal> { Value = 42M };
            IStrongBox clone = container.Clone() as IStrongBox;
            NotNull(clone);
            Equal(42M, clone.Value);
            clone.Value = 2M;
            Equal(2M, clone.Value);
        }

        [Fact]
        public static void Exchange()
        {
            var container = new Atomic<decimal> { Value = 42M };
            False(container.CompareExchange(10M, 43M, out var value));
            Equal(42M, value);
            Equal(42M, container.Value);
        }

        [Fact]
        public static void StringConversion()
        {
            var container = new Atomic<decimal> { Value = 42M };
            Equal(42M.ToString(), container.ToString());
        }
    }
}