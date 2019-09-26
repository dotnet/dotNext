using System;
using System.Threading;
using Xunit;

namespace DotNext.Diagnostics
{
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
    }
}
