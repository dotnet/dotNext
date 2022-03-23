using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNext.Threading.Channels;

using IO;

internal sealed class PersistentChannelWriter<T> : ChannelWriter<T>, IChannelInfo, IDisposable
    where T : notnull
{
    private const string StateFileName = "writer.state";
    private readonly IChannelWriter<T> writer;
    private readonly FileCreationOptions fileOptions;
    private AsyncLock writeLock;
    private PartitionStream? writeTopic;
    private ChannelCursor cursor;

    internal PersistentChannelWriter(IChannelWriter<T> writer, bool singleWriter, long initialSize)
    {
        writeLock = singleWriter ? default : AsyncLock.Exclusive();
        this.writer = writer;
        fileOptions = new FileCreationOptions(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous | FileOptions.WriteThrough, initialSize);
        cursor = new ChannelCursor(writer.Location, StateFileName);
    }

    public long Position => cursor.Position;

    public override bool TryComplete(Exception? error = null) => writer.TryComplete(error);

    public override bool TryWrite(T item) => false;

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken token = default)
        => token.IsCancellationRequested ? ValueTask.FromCanceled<bool>(token) : ValueTask.FromResult(true);

    private PartitionStream Partition => writer.GetOrCreatePartition(ref cursor, ref writeTopic, fileOptions, false);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public override async ValueTask WriteAsync(T item, CancellationToken token)
    {
        using (await writeLock.AcquireAsync(token).ConfigureAwait(false))
        {
            if (writer.Completion.IsCompleted)
                throw new ChannelClosedException();

            var partition = Partition;
            await writer.SerializeAsync(item, partition, token).ConfigureAwait(false);
            await partition.FlushAsync(token).ConfigureAwait(false);
            await cursor.AdvanceAsync(partition.Position, token).ConfigureAwait(false);
        }

        writer.MessageReady();
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            writeTopic?.Dispose();
            writeTopic = null;
            cursor.Dispose();
        }

        writeLock.Dispose();
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PersistentChannelWriter() => Dispose(false);
}