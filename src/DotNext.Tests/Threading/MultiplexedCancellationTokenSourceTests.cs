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

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public static void MultipleTokens(int count)
    {
        var sources = new CancellationTokenSource[count];
        Span.Initialize(sources);

        using var source = CancellationToken.Combine([.. sources.Select(static source => source.Token)]);
        foreach (var cts in sources)
        {
            NotEqual(cts.Token, source.Token);
        }
        
        sources[0].Cancel();
        Equal(source.CancellationOrigin, sources[0].Token);

        for (var i = 1; i < count; i++)
        {
            NotEqual(source.CancellationOrigin, sources[i].Token);
        }
    }
}