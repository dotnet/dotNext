using System.Diagnostics;

namespace DotNext.Threading.Channels;

using IO;

internal interface IChannelReader<T> : IChannel, IDisposable
{
    private const string OutputTypeMeterAttribute = "dotnext.threading.channels.persistentchannel.output";

    private protected static void SetTags(ref TagList tags)
        => tags.Add(OutputTypeMeterAttribute, typeof(T).Name);

    long WrittenCount { get; }

    Task WaitToReadAsync(CancellationToken token);

    void RollbackRead();

    ValueTask<T> DeserializeAsync(Partition input, CancellationToken token);
}