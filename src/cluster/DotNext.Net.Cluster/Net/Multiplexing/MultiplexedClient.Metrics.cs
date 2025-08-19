using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedClient
{
    private static readonly UpDownCounter<int> streamCount;
    
    static MultiplexedClient()
    {
        var meter = new Meter("DotNext.Net.Multiplexing.Client");
        streamCount = meter.CreateUpDownCounter<int>("streams-count", description: "Number of Streams");
    }
}