using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

public sealed class GaugeObserverTests : Test
{
    [Fact]
    public static void ObserveValue()
    {
        const string meterName = "DotNext.Test.Gauge";
        const string gaugeName = "gauge";
        
        var observer = new GaugeObserver<double>(Filter);
        False(observer.IsCompleted);
        using (var meter = new Meter(meterName))
        {
            var gauge = meter.CreateGauge<double>(gaugeName);

            using var listener = new MeterListenerBuilder()
                .Observe(static instr => instr.Meter.Name is meterName, observer)
                .Build();

            listener.Start();

            Equal(0D, observer.Value);

            gauge.Record(42D);
            Equal(42D, observer.Value);

            gauge.Record(56D, [new("key", "value")]);
            Equal(42D, observer.Value);
        }

        True(observer.IsCompleted);

        static bool Filter(Gauge<double> histogram, ReadOnlySpan<KeyValuePair<string, object>> tags)
            => tags is [];
    }
}