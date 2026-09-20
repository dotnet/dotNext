using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

public sealed class GaugeObserverTests : InstrumentObserverTest
{
    [Fact]
    public static void ObserveValue()
    {
        const string meterName = "DotNext.Test.Gauge";
        const string gaugeName = "gauge";

        var observer = new GaugeObserver<double>(Filter);
        var observer2 = new GaugeObserver<double>();
        False(observer.IsCompleted);
        False(observer2.IsCompleted);
        using (var meter = new Meter(meterName))
        {
            var gauge = meter.CreateGauge<double>(gaugeName);

            using var listener = new MeterListenerBuilder()
                .Observe(IsCorrectMeter, observer)
                .Build();

            using var listener2 = new MeterListenerBuilder()
                .Observe(IsCorrectMeter, observer2)
                .Build();

            listener.Start();
            listener2.Start();

            Equal(0D, observer.Value);
            Equal(0D, observer2.Value);

            gauge.Record(42D);
            Equal(42D, observer.Value);
            Equal(42D, observer2.Value);

            gauge.Record(56D, [new("key", "value")]);
            Equal(42D, observer.Value);
            Equal(56D, observer2.Value);
        }

        True(observer.IsCompleted);

        static bool IsCorrectMeter(Instrument instr) => instr.Meter.Name is meterName;
    }
}