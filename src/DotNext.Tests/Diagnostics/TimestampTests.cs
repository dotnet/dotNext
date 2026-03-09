namespace DotNext.Diagnostics;

public sealed class TimestampTests : Test
{
    [Fact]
    public static void MeasurementTest()
    {
        var ts = new Timestamp();
        Thread.Sleep(10);
        True(ts.Elapsed >= TimeSpan.FromMilliseconds(10));
        True(ts.ElapsedTicks >= TimeSpan.FromMicroseconds(10).Ticks);
        True(new Timestamp() > ts);
        True(new Timestamp() != ts);
        var other = ts;
        True(other == ts);
    }

    [Fact]
    public static void ComparisonOperators()
    {
        var ts = new Timestamp();
        var ts2 = ts;
        Equal(ts, ts2);
        False(ts < ts2);
        False(ts > ts2);
        Thread.Sleep(30);
        ts2 = new Timestamp();
        NotEqual(ts, ts2);
        True(ts2 > ts);
        False(ts2 < ts);
    }

    [Fact]
    public static void Equality()
    {
        var ts = new Timestamp();
        object other = ts;
        Equal(ts, other);
    }

    [Fact]
    public static void Conversion()
    {
        var ts = new Timestamp();
        Equal(ts.Value, (TimeSpan)ts);

        ts = new(TimeSpan.FromSeconds(1.2D));
        Equal(TimeSpan.FromSeconds(1.2D), ts.Value);
    }

    [Fact]
    public static void VolatileAccess()
    {
        var ts = new Timestamp();
        Equal(ts, Timestamp.VolatileRead(ref ts));
        Timestamp.VolatileWrite(ref ts, default(Timestamp));
        Equal(default(Timestamp), ts);
    }

    [Fact]
    public static void ArithmeticOperators()
    {
        var current = new Timestamp();
        var result = current + TimeSpan.FromMilliseconds(100);
        Equal(TimeSpan.FromMilliseconds(100), result.Value - current.Value);

        result = current - TimeSpan.FromMilliseconds(100);
        Equal(TimeSpan.FromMilliseconds(100), current.Value - result.Value);
    }

    [Fact]
    public static void CheckedArithmeticOperators()
    {
        Throws<OverflowException>(static () => checked(new Timestamp() - TimeSpan.MaxValue));
        Throws<OverflowException>(static () => checked(new Timestamp() + TimeSpan.MaxValue));
    }

    [Fact]
    public static void DefaultTimestamp()
    {
        var ts = new Timestamp();
        NotEqual(default(Timestamp), ts);
        True(default(Timestamp).IsEmpty);
    }

    [Fact]
    public static void PointInTime()
    {
        True(default(Timestamp).IsPast);
        True(default(Timestamp).IsPast(TimeProvider.System));
        False(default(Timestamp).IsFuture);
        False(default(Timestamp).IsFuture(TimeProvider.System));

        var ts = new Timestamp() + TimeSpan.FromHours(1);
        True(ts.IsFuture);
        True(ts.IsFuture(TimeProvider.System));
        False(ts.IsPast);
        False(ts.IsPast(TimeProvider.System));
    }

    [Fact]
    public static void AddSubtractZero()
    {
        var ts = default(Timestamp) + TimeSpan.Zero;
        True(ts.IsEmpty);

        ts = default(Timestamp) - TimeSpan.Zero;
        True(ts.IsEmpty);
    }

    [Fact]
    public static void ElapsedMilliseconds()
    {
        var ts = new Timestamp();
        Thread.Sleep(10);
        var e = ts.ElapsedMilliseconds;
        True(e >= 10D);
    }

    [Fact]
    public static void CompareExchange()
    {
        var current = new Timestamp();
        var value = default(Timestamp);

        Equal(default, Timestamp.CompareExchange(ref value, current, default));
        Equal(current, value);
    }
}