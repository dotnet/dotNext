using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncLazyTests : Test
    {
        [Fact]
        public static void PrecomputedValue()
        {
            var lazy = new AsyncLazy<int>(2);
            True(lazy.IsValueCreated);
            False(lazy.Reset());
            True(lazy.Task.IsCompletedSuccessfully);
            Equal(2, lazy.Task.Result);
        }

        private static async Task<long> MaxValue()
        {
            await Task.Delay(100);
            return 42L;
        }

        [Fact]
        public static async Task LazyComputation()
        {
            var lazy = new AsyncLazy<long>(MaxValue);
            False(lazy.IsValueCreated);
            Equal(42L, await lazy);
            True(lazy.IsValueCreated);
            Equal(42L, lazy.Value.Value);
        }
    }
}
