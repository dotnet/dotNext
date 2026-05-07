namespace DotNext.Runtime.ExceptionServices;

public sealed class ExceptionAggregatorTests : Test
{
    [Fact]
    public static void DefaultValue()
    {
        var aggregator = new ExceptionAggregator();
        True(aggregator.IsEmpty);
        Null(aggregator.CreateException());
    }

    [Fact]
    public static void SingleException()
    {
        var expected = new Exception();
        var aggregator = new ExceptionAggregator();
        aggregator.Add(expected);
        False(aggregator.IsEmpty);
        Same(expected, aggregator.CreateException());
    }

    [Fact]
    public static void MultipleExceptions()
    {
        var e1 = new ArithmeticException();
        var e2 = new InvalidOperationException();
        var aggregator = new ExceptionAggregator();
        aggregator += e1;
        aggregator += e2;

        var e = IsType<AggregateException>(aggregator.CreateException());
        Contains(e1, e.InnerExceptions);
        Contains(e2, e.InnerExceptions);
    }
}