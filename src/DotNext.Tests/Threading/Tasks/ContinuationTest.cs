using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
    public sealed class ContinuationTest : Assert
    {
        [Fact]
        public static async Task OnCompletedContinuation()
        {
            var t = Task.Delay(50);
            var t2 = await t.OnCompleted();
            Equal(t, t2);
            True(t.IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task OnCompletedContinuation2()
        {
            var t = Task<int>.Factory.StartNew(() =>
            {
                Thread.Sleep(50);
                return 42;
            });
            var t2 = await t.OnCompleted();
            Equal(t, t2);
            True(t.IsCompletedSuccessfully);
            Equal(42, t2.Result);
        }
    }
}