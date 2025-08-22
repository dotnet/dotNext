using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedClient : IStreamMetrics
{
    private static readonly UpDownCounter<long> StreamCount;
    
    static MultiplexedClient()
    {
        var meter = new Meter("DotNext.Net.Multiplexing.Client");
        StreamCount = meter.CreateUpDownCounter<long>("streams-count", description: "Number of Streams");
    }

    static void IStreamMetrics.ChangeStreamCount(long delta, in TagList measurementTags)
        => StreamCount.Add(delta, measurementTags);
}