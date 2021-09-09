using System.Diagnostics.CodeAnalysis;
using Xunit;

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
                counter.Increment();
                counter.Increment();
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
    }
}
