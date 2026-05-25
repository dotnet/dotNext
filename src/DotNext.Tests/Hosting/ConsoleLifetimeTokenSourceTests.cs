namespace DotNext.Hosting;

public sealed class ConsoleLifetimeTokenSourceTests : Test
{
    [Fact]
    public static void CreateDispose()
    {
        SkipUnless(ConsoleLifetimeTokenSource.IsSupported, "The feature is not supported on the current OS.");
        
        using var source = new ConsoleLifetimeTokenSource();
        False(source.IsCancellationRequested);
    }
}