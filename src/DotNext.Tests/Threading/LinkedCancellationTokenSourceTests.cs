using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class LinkedCancellationTokenSourceTests : Test
    {
        [Fact]
        public static async Task LinkedCancellation()
        {
            using var source1 = new CancellationTokenSource();
            using var source2 = new CancellationTokenSource();
            var token = source1.Token;
            using var linked = token.LinkTo(source2.Token);
            NotNull(linked);

            source1.CancelAfter(100);
            try
            {
                await Task.Delay(DefaultTimeout, linked.Token);
            }
            catch (OperationCanceledException e)
            {
                Equal(e.CancellationToken, linked.Token);
                NotEqual(source1.Token, e.CancellationToken);
                Equal(linked.CancellationOrigin, source1.Token);
            }
        }

        [Fact]
        public static async Task DirectCancellation()
        {
            using var source1 = new CancellationTokenSource();
            using var source2 = new CancellationTokenSource();
            var token = source1.Token;
            using var linked = token.LinkTo(source2.Token);
            NotNull(linked);

            linked.CancelAfter(100);
            try
            {
                await Task.Delay(DefaultTimeout, linked.Token);
            }
            catch (OperationCanceledException e)
            {
                Equal(e.CancellationToken, linked.Token);
                NotEqual(source1.Token, e.CancellationToken);
                Equal(linked.CancellationOrigin, linked.Token);
            }
        }
    }
}