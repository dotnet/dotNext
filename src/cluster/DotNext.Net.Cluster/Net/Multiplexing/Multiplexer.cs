using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

using Threading;

internal abstract class Multiplexer(
    ConcurrentDictionary<ulong, StreamHandler> streams,
    IProducerConsumerCollection<ProtocolCommand> commands,
    UpDownCounter<int> streamCounter,
    in TagList measurementTags,
    CancellationToken token) : Disposable
{
    protected readonly UpDownCounter<int> streamCounter = streamCounter;
    protected readonly TagList measurementTags = measurementTags;
    protected readonly ConcurrentDictionary<ulong, StreamHandler> streams = streams;
    protected readonly PoolingTimeoutSource timeoutSource = new(token);
    protected readonly IProducerConsumerCollection<ProtocolCommand> commands = commands;
    
    public CancellationToken Token => token;

    protected void ChangeStreamCount(int delta = 1) => streamCounter.Add(delta, in measurementTags);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timeoutSource.Dispose();
        }
        
        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore() => timeoutSource.DisposeAsync();

    public new ValueTask DisposeAsync() => base.DisposeAsync();
}