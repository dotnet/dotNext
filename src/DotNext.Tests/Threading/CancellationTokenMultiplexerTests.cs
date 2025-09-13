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
        NotEqual(new(true), scope.Token);
    }
    
    [Fact]
    public static async Task CanceledImmediatelyAsync()
    {
        var multiplexer = new CancellationTokenMultiplexer();
        await using var scope = multiplexer.Combine([new(true), new(true)]);
        True(scope.Token.IsCancellationRequested);
        Equal(new(true), scope.CancellationOrigin);
        NotEqual(new(true), scope.Token);
    }

    [Fact]
    public static void CheckPooling()
    {
        CancellationToken token;
        var multiplexer = new CancellationTokenMultiplexer { MaximumRetained = int.MaxValue };
        using (var scope = multiplexer.Combine([new(false)]))
        {
            token = scope.Token;
        }
        
        // rent again
        using (var scope = multiplexer.Combine([new(false)]))
        {
            Equal(token, scope.Token);
        }
    }
}