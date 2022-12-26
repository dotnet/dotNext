using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage]
    public sealed class SpawningAsyncTaskMethodBuilderTests : Test
    {
        [Fact]
        public static async Task ForkAsyncMethodWithResult()
        {
            Equal(42, await Sum(40, 2, Thread.CurrentThread.ManagedThreadId));

            [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
            static async Task<int> Sum(int x, int y, int callerThreadId)
            {
                NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

                await Task.Yield();
                return x + y;
            }
        }

        [Fact]
        public static async Task ForkAsyncMethodWithoutResult()
        {
            await CheckThreadId(Thread.CurrentThread.ManagedThreadId);

            [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
            static async Task CheckThreadId(int callerThreadId)
            {
                NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

                await Task.Yield();
            }
        }
    }
}