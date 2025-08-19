using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

public sealed class UpDownCounterObserverTests : Test
{
    [Fact]
    public static void ObserveValue()
    {
        const string meterName = "DotNext.Test.UpDownCounter";
        const string counterName = "counter";
        
        using var meter = new Meter(meterName);
        var counter = meter.CreateUpDownCounter<int>(counterName);

        var observer = new UpDownCounterObserver<int>(Filter);
        using var listener = new MeterListenerBuilder()
            .Observe(static instr => instr.Meter.Name is meterName, observer)
            .Build();

        listener.Start();

        Equal(0, observer.Value);
        
        counter.Add(41);
        counter.Add(1);
        Equal(42, observer.Value);

        counter.Add(56, [new("key", "value")]);
        Equal(42, observer.Value);

        static bool Filter(UpDownCounter<int> counter, ReadOnlySpan<KeyValuePair<string, object>> tags)
            => tags is [];
    }
}