namespace DotNext.Hosting;

public sealed class ConsoleLifetimeTokenSourceTests : Test
{
    [Fact]
    public static void CreateDispose()
    {
        using var source = new ConsoleLifetimeTokenSource();
        False(source.IsCancellationRequested);
    }
}