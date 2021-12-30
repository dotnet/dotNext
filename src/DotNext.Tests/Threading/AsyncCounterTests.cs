using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncCounterTests : Test
    {
        [Fact]
        public static async Task SignalAndWait()
        {
            using (var counter = new AsyncCounter())
            {
                Equal(0, counter.Value);
                counter.Increment(2L);
                counter.Increment(0L);
                Equal(2, counter.Value);
                True(await counter.WaitAsync(TimeSpan.Zero));
                True(await counter.WaitAsync(TimeSpan.Zero));
                False(await counter.WaitAsync(TimeSpan.Zero));
                Equal(0, counter.Value);
            }
            using (IAsyncEvent counter = new AsyncCounter())
            {
                False(counter.IsSet);
                True(counter.Signal());
                True(counter.Signal());
                True(counter.IsSet);
                True(await counter.WaitAsync(TimeSpan.Zero));
                True(await counter.WaitAsync(TimeSpan.Zero));
                False(await counter.WaitAsync(TimeSpan.Zero));
                False(counter.IsSet);
            }
        }

        [Fact]
        public static void InvalidDeltaValue()
        {
            using var counter = new AsyncCounter();

            Throws<ArgumentOutOfRangeException>(() => counter.Increment(-1L));
        }

        [Fact]
        public static void CounterOverflow()
        {
            using var counter = new AsyncCounter(initialValue: long.MaxValue);

            Throws<OverflowException>(counter.Increment);
        }
    }
}
