using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

internal abstract partial class Multiplexer(
    ConcurrentDictionary<ulong, MultiplexedStream> streams,
    IProducerConsumerCollection<ProtocolCommand> commands,
    UpDownCounter<int> streamCounter,
    in TagList measurementTags,
    CancellationToken token) : Disposable
{
    protected readonly UpDownCounter<int> streamCounter = streamCounter;
    protected readonly TagList measurementTags = measurementTags;
    protected readonly ConcurrentDictionary<ulong, MultiplexedStream> streams = streams;
    protected readonly IProducerConsumerCollection<ProtocolCommand> commands = commands;

    protected void ChangeStreamCount(int delta = 1) => streamCounter.Add(delta, in measurementTags);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            source.Dispose();
        }
        
        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore() => source.DisposeAsync();

    public new ValueTask DisposeAsync() => base.DisposeAsync();
}