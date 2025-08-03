using System.Collections.Concurrent;

namespace DotNext.Net.Multiplexing;

using Threading;

internal abstract class Multiplexer(
    ConcurrentDictionary<ulong, StreamHandler> streams,
    IProducerConsumerCollection<ProtocolCommand> commands,
    CancellationToken token) : IAsyncDisposable
{
    protected readonly ConcurrentDictionary<ulong, StreamHandler> streams = streams;
    protected readonly PoolingTimeoutSource timeoutSource = new(token);
    protected readonly IProducerConsumerCollection<ProtocolCommand> commands = commands;
    protected readonly CancellationToken token = token;

    public ValueTask DisposeAsync() => timeoutSource.DisposeAsync();
}