namespace DotNext.Threading;

public class CancellationTokenMultiplexerTests : Test
{
    [Fact]
    public static void CanceledImmediately()
    {
        var multiplexer = new CancellationTokenMultiplexer();
        using var scope = multiplexer.Combine([new(true), new(true)]);
        True(scope.Token.IsCancellationRequested);
        Equal(new(true), scope.CancellationOrigin);
        Equal(new(true), scope.Token);
    }
    
    [Fact]
    public static async Task CanceledImmediatelyAsync()
    {
        var multiplexer = new CancellationTokenMultiplexer();
        await using var scope = multiplexer.Combine([new(true), new(true)]);
        True(scope.Token.IsCancellationRequested);
        Equal(new(true), scope.CancellationOrigin);
        Equal(new(true), scope.Token);
    }

    [Fact]
    public static void CheckPooling()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken token;
        var multiplexer = new CancellationTokenMultiplexer { MaximumRetained = int.MaxValue };
        Equal(int.MaxValue, multiplexer.MaximumRetained);
        using (var scope = multiplexer.Combine(cts.Token, cts.Token, cts.Token))
        {
            token = scope.Token;
        }
        
        // rent again
        using (var scope = multiplexer.Combine(cts.Token, cts.Token, cts.Token))
        {
            Equal(token, scope.Token);
        }
    }

    [Fact]
    public async Task CheckPoolingNonInterference()
    {
        var multiplexer = new CancellationTokenMultiplexer();

        using var cts = new CancellationTokenSource();

        await multiplexer.Combine(cts.Token, cts.Token, cts.Token).DisposeAsync();

        // same source is reused from pool, but should now not be associated with cts.
        await using var combined = multiplexer.Combine(CancellationToken.None, CancellationToken.None, CancellationToken.None);

        await cts.CancelAsync();

        False(combined.Token.IsCancellationRequested);
    }

    [Fact]
    public static void ExtraListOverflow()
    {
        Span<CancellationToken> tokens = new CancellationToken[20];
        foreach (ref var token in tokens)
        {
            token = new(true);
        }
        
        var multiplexer = new CancellationTokenMultiplexer();
        
        using var scope = multiplexer.Combine(tokens);
        True(scope.Token.IsCancellationRequested);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task TimeOut(int timeout)
    {
        using var cts = new CancellationTokenSource();
        var multiplexer = new CancellationTokenMultiplexer();

        await using var scope = multiplexer.Combine(TimeSpan.FromMilliseconds(timeout), cts.Token);
        await scope.Token.WaitAsync();

        Equal(scope.Token, scope.CancellationOrigin);
        True(scope.IsTimedOut);
        NotEqual(scope.Token, cts.Token);
    }

    [Fact]
    public static async Task LazyTimeout()
    {
        var multiplexer = new CancellationTokenMultiplexer();

        await using var scope = multiplexer.CombineAndSetTimeoutLater();
        False(scope.Token.IsCancellationRequested);

        scope.Timeout = TimeSpan.FromMilliseconds(0);
        await scope.Token.WaitAsync();
        
        Equal(scope.Token, scope.CancellationOrigin);
        True(scope.IsTimedOut);
    }

    [Fact]
    public static void DefaultScope()
    {
        using var scope = default(CancellationTokenMultiplexer.Scope);
        CheckDefaultScope(scope);
    }
    
    [Fact]
    public static async Task DefaultScopeAsync()
    {
        await using var scope = default(CancellationTokenMultiplexer.Scope);
        False(scope.IsTimedOut);
    }

    [Fact]
    public static void DefaultScopeWithTimeout()
    {
        using var scope = default(CancellationTokenMultiplexer.ScopeWithTimeout);
        CheckDefaultScope(scope);
    }
    
    [Fact]
    public static async Task DefaultScopeWithTimeoutAsync()
    {
        await using var scope = default(CancellationTokenMultiplexer.ScopeWithTimeout);
        False(scope.IsTimedOut);
    }

    private static void CheckDefaultScope<TScope>(TScope scope)
        where TScope : struct, IMultiplexedCancellationTokenSource
    {
        False(scope.Token.IsCancellationRequested);
        Equal(scope.Token, scope.CancellationOrigin);
    }

    [Fact]
    public static async Task CompleteAndCheckConcurrently()
    {
        var multiplexer = new CancellationTokenMultiplexer();

        var scope = multiplexer.CombineAndSetTimeoutLater();
        var source = new TaskCompletionSource<bool>();
        await using var registration = scope.Token.Register(() =>
        {
            source.SetResult(scope.IsTimedOut);
        });

        scope.Timeout = TimeSpan.Zero;
        True(await source.Task);

        await scope.DisposeAsync();
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public static void BoundedPool(int maximumRetained)
    {
        var multiplexer = new CancellationTokenMultiplexer { MaximumRetained = maximumRetained };
        Equal(maximumRetained, multiplexer.MaximumRetained);
        for (var i = 0; i < maximumRetained << 1; i++)
        {
            var scope = multiplexer.Combine(CancellationToken.None, CancellationToken.None, CancellationToken.None);
            scope.Dispose();
        }
    }
    
    [Fact]
    public static async Task WaitForCancellationSingleToken()
    {
        var multiplexer = new CancellationTokenMultiplexer();
        var cts = new CancellationTokenSource();
        var task = multiplexer.WaitAnyAsync(cts.Token).AsTask();
        False(task.IsCompletedSuccessfully);
        
        await cts.CancelAsync();
        Equal(cts.Token, await task);
    }
    
    [Fact]
    public static async Task WaitForCancellationTwoTokens()
    {
        var multiplexer = new CancellationTokenMultiplexer();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        var task = multiplexer.WaitAnyAsync(cts1.Token, cts2.Token).AsTask();
        False(task.IsCompletedSuccessfully);
        
        await cts2.CancelAsync();
        await cts1.CancelAsync();
        Equal(cts2.Token, await task);
    }

    [Fact]
    public static async Task WaitForCancellationMultipleTokens()
    {
        var multiplexer = new CancellationTokenMultiplexer();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        using var cts3 = new CancellationTokenSource();
        var task = multiplexer.WaitAnyAsync(cts1.Token, cts2.Token, cts3.Token).AsTask();
        False(task.IsCompletedSuccessfully);

        await cts3.CancelAsync();
        await cts2.CancelAsync();
        await cts1.CancelAsync();
        Equal(cts3.Token, await task);
    }
}