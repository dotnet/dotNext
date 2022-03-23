namespace DotNext.Threading.Channels;

using IO;

internal interface IChannelReader<T> : IChannel, IDisposable
{
    long WrittenCount { get; }

    Task WaitToReadAsync(CancellationToken token);

    void RollbackRead();

    ValueTask<T> DeserializeAsync(PartitionStream input, CancellationToken token);
}