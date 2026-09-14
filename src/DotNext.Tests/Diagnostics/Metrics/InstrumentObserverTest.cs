using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

public abstract class InstrumentObserverTest : Test
{
    protected static bool Filter<T>(Instrument<T> counter, ReadOnlySpan<KeyValuePair<string, object>> tags)
        where T : struct
        => tags is [];
}