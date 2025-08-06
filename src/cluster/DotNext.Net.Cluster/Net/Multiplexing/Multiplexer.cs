using System.Collections.Concurrent;

namespace DotNext.Net.Multiplexing;

using Threading;

internal abstract class Multiplexer(
    ConcurrentDictionary<ulong, StreamHandler> streams,
    IProducerConsumerCollection<ProtocolCommand> commands,
    CancellationToken token) : Disposable
{
    protected readonly ConcurrentDictionary<ulong, StreamHandler> streams = streams;
    protected readonly PoolingTimeoutSource timeoutSource = new(token);
    protected readonly IProducerConsumerCollection<ProtocolCommand> commands = commands;
    
    public CancellationToken Token => token;

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