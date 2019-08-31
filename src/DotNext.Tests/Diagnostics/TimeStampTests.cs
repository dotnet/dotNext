using System;
using System.Threading;
using Xunit;

namespace DotNext.Diagnostics
{
    public sealed class TimeStampTests : Assert
    {
        [Fact]
        public static void MeasurementTest()
        {
            var ts = TimeStamp.Current;
            Thread.Sleep(10);
            True(ts.Elapsed >= TimeSpan.FromMilliseconds(10));
            True(TimeStamp.Current > ts);
            True(TimeStamp.Current != ts);
            var other = ts;
            True(other == ts);
        }
    }
}
