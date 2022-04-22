namespace DotNext.Threading.Channels;

using IO;

internal interface IChannelWriter<T> : IChannel
{
    void MessageReady();

    ValueTask SerializeAsync(T input, Partition output, CancellationToken token);

    bool TryComplete(Exception? e = null);
}