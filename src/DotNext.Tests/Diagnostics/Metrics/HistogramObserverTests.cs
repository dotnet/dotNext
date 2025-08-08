using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

public sealed class HistogramObserverTests : Test
{
    [Fact]
    public static void ObserveValue()
    {
        const string meterName = "DotNext.Test.Histogram";
        const string histogramName = "histogram";
        
        var observer = new HistogramObserver<int>(Filter);
        False(observer.IsCompleted);
        using (var meter = new Meter(meterName))
        {
            var histogram = meter.CreateHistogram<int>(histogramName);

            using var listener = new MeterListenerBuilder()
                .Observe(static instr => instr.Meter.Name is meterName, observer)
                .Build();

            listener.Start();

            Equal(0, observer.Value);

            histogram.Record(42);
            Equal(42, observer.Value);

            histogram.Record(56, [new("key", "value")]);
            Equal(42, observer.Value);
        }

        True(observer.IsCompleted);

        static bool Filter(Histogram<int> histogram, ReadOnlySpan<KeyValuePair<string, object>> tags)
            => tags is [];
    }
}