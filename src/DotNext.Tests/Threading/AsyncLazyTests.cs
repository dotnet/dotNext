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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task LazyComputation(bool resettable)
        {
            var lazy = new AsyncLazy<long>(MaxValue, resettable);
            False(lazy.IsValueCreated);
            Equal(42L, await lazy);
            True(lazy.IsValueCreated);
            Equal(42L, lazy.Value.Value);

            static async Task<long> MaxValue()
            {
                await Task.Delay(100);
                return 42L;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task ExceptionDuringComputation(bool resettable)
        {
            var lazy = new AsyncLazy<long>(ThrowException, resettable);
            False(lazy.IsValueCreated);
            await ThrowsAsync<ArithmeticException>(() => lazy.Task);
            True(lazy.IsValueCreated);

            static async Task<long> ThrowException()
            {
                await Task.Delay(100);
                throw new ArithmeticException();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task CancellationDuringComputation(bool resettable)
        {
            var lazy = new AsyncLazy<long>(MaxValue, resettable);
            False(lazy.IsValueCreated);
            await ThrowsAsync<TaskCanceledException>(() => lazy.WithCancellation(new(true)));
            False(lazy.IsValueCreated);

            static async Task<long> MaxValue(CancellationToken token)
            {
                await Task.Delay(100, token);
                return 42L;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task CancellationDuringComputation2(bool resettable)
        {
            var lazy = new AsyncLazy<long>(MaxValue, resettable);
            False(lazy.IsValueCreated);
            await ThrowsAsync<TaskCanceledException>(() => lazy.Task);
            False(lazy.IsValueCreated);

            static async Task<long> MaxValue()
            {
                await Task.Delay(100, new CancellationToken(true));
                return 42L;
            }
        }
    }
}
