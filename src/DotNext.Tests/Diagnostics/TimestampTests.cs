using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Xunit;

namespace DotNext.Diagnostics
{
    [ExcludeFromCodeCoverage]
    public sealed class TimestampTests : Xunit.Assert
    {
        [Fact]
        public static void MeasurementTest()
        {
            var ts = Timestamp.Current;
            Thread.Sleep(10);
            True(ts.Elapsed >= TimeSpan.FromMilliseconds(10));
            True(Timestamp.Current > ts);
            True(Timestamp.Current != ts);
            var other = ts;
            True(other == ts);
        }

        [Fact]
        public static void Operators()
        {
            var ts = Timestamp.Current;
            var ts2 = ts;
            Equal(ts, ts2);
            False(ts < ts2);
            False(ts > ts2);
            Thread.Sleep(30);
            ts2 = Timestamp.Current;
            NotEqual(ts, ts2);
            True(ts2 > ts);
            False(ts2 < ts);
        }
    }
}
