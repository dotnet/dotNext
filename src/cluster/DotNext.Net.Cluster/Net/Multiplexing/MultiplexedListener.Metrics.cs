using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedListener : IStreamMetrics
{
    private const string ClientAddressMeterAttribute = "dotnext.net.multiplexing.client.address";

    private static readonly UpDownCounter<long> StreamCount, PendingStreamCount;
    
    static MultiplexedListener()
    {
        var meter = new Meter("DotNext.Net.Multiplexing.Server");
        StreamCount = meter.CreateUpDownCounter<long>("streams-count", description: "Number of Streams");
        PendingStreamCount = meter.CreateUpDownCounter<long>("pending-streams-count", description: "Number of Pending Streams");
    }

    private readonly TagList measurementTags;

    static void IStreamMetrics.ChangeStreamCount(long delta, in TagList measurementTags)
        => StreamCount.Add(delta, measurementTags);
}