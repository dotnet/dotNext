using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncCounterTests : Assert
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
                True(await counter.Wait(TimeSpan.Zero));
                True(await counter.Wait(TimeSpan.Zero));
                False(await counter.Wait(TimeSpan.Zero));
                Equal(0, counter.Value);
            }
            using (IAsyncEvent counter = new AsyncCounter())
            {
                False(counter.IsSet);
                True(counter.Signal());
                True(counter.Signal());
                True(counter.IsSet);
                True(await counter.Wait(TimeSpan.Zero));
                True(await counter.Wait(TimeSpan.Zero));
                False(await counter.Wait(TimeSpan.Zero));
                False(counter.IsSet);
            }
        }
    }
}
