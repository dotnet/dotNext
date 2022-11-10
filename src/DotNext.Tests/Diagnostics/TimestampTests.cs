using System.Diagnostics.CodeAnalysis;

namespace DotNext.Diagnostics
{
    [ExcludeFromCodeCoverage]
    public sealed class TimestampTests : Test
    {
        [Fact]
        public static void MeasurementTest()
        {
            var ts = new Timestamp();
            Thread.Sleep(10);
            True(ts.Elapsed >= TimeSpan.FromMilliseconds(10));
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
            Equal(ts.Value, ts);
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
            False(default(Timestamp).IsFuture);

            var ts = new Timestamp() + TimeSpan.FromHours(1);
            True(ts.IsFuture);
            False(ts.IsPast);
        }

        [Fact]
        public static void AddSubtractZero()
        {
            var ts = default(Timestamp) + TimeSpan.Zero;
            True(ts.IsEmpty);

            ts = default(Timestamp) - TimeSpan.Zero;
            True(ts.IsEmpty);
        }
    }
}
