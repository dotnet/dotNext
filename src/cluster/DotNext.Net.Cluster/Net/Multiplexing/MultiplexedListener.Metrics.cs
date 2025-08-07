using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedListener
{
    private const string ClientAddressMeterAttribute = "dotnext.net.multiplexing.client.address";
    
    private static readonly UpDownCounter<int> streamCount;
    
    static MultiplexedListener()
    {
        var meter = new Meter("DotNext.Net.Multiplexing.Server");
        streamCount = meter.CreateUpDownCounter<int>("streams-count", description: "Number of Streams");
    }

    private readonly TagList measurementTags;
}