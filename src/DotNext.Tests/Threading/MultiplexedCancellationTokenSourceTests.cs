namespace DotNext.Threading;

public sealed class MultiplexedCancellationTokenSourceTests : Test
{
    [Fact]
    public static void SingleTokenSource()
    {
        Same(IMultiplexedCancellationTokenSource.Create(canceled: false), CancellationToken.Combine());
        Same(IMultiplexedCancellationTokenSource.Create(canceled: false), CancellationToken.Combine(new CancellationToken(canceled: false)));
        Same(IMultiplexedCancellationTokenSource.Create(canceled: true), CancellationToken.Combine(new CancellationToken(canceled: true)));
    }

    [Fact]
    public static void CustomTokenSource()
    {
        using var cts = new CancellationTokenSource();
        using var source = CancellationToken.Combine(cts.Token);
        Equal(cts.Token, source.Token);
        Equal(cts.Token, source.CancellationOrigin);
    }

    [Fact]
    public static void MultipleTokens()
    {
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        
        using var source = CancellationToken.Combine(cts1.Token, cts2.Token);
        NotEqual(cts1.Token, source.Token);
        NotEqual(cts2.Token, source.Token);
        
        cts1.Cancel();
        Equal(source.CancellationOrigin, cts1.Token);
        NotEqual(source.CancellationOrigin, cts2.Token);
    }
}