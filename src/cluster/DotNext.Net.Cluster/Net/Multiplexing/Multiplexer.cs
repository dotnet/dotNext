using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

internal abstract partial class Multiplexer<T>(
    ConcurrentDictionary<uint, MultiplexedStream> streams,
    IProducerConsumerCollection<ProtocolCommand> commands,
    in TagList measurementTags,
    CancellationToken token) : Disposable
    where T : IStreamMetrics
{
    protected readonly TagList measurementTags = measurementTags;
    protected readonly ConcurrentDictionary<uint, MultiplexedStream> streams = streams;
    protected readonly IProducerConsumerCollection<ProtocolCommand> commands = commands;

    protected void ChangeStreamCount(long delta = 1) => T.ChangeStreamCount(delta, measurementTags);

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