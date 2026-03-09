using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotNext.Net.Multiplexing;

internal abstract partial class Multiplexer(
    ConcurrentDictionary<uint, MultiplexedStream> streams,
    IProducerConsumerCollection<ProtocolCommand> commands)
{
    protected readonly IProducerConsumerCollection<ProtocolCommand> Commands = commands;
    protected readonly ConcurrentDictionary<uint, MultiplexedStream> Streams = streams;
}

internal abstract class Multiplexer<T>(
    ConcurrentDictionary<uint, MultiplexedStream> streams,
    IProducerConsumerCollection<ProtocolCommand> commands) : Multiplexer(streams, commands)
    where T : IStreamMetrics
{
    public required TagList MeasurementTags;

    protected void ChangeStreamCount(long delta = 1) => T.StreamCount.Add(delta, MeasurementTags);
}