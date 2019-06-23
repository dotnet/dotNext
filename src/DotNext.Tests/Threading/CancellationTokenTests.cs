using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class CancellationTokenTests : Assert
    {
        [Fact]
        public static async Task WaitForCancellationNoThrow()
        {
            using(var source = new CancellationTokenSource(400))
            {
                await source.Token.AsTask().ConfigureAwait(false);
                True(source.IsCancellationRequested);
            }
        }

        [Fact]
        public static async Task WaitForCancellation()
        {
            using (var source = new CancellationTokenSource(400))
            {
                await ThrowsAsync<TaskCanceledException>(() => source.Token.AsTask(true)).ConfigureAwait(false);
                True(source.IsCancellationRequested);
            }
        }
    }
}
