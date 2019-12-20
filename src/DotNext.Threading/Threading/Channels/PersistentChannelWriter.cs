using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    internal sealed class PersistentChannelWriter<T> : ChannelWriter<T>, IChannelInfo, IDisposable
    {
        private const string StateFileName = "writer.state";
        private readonly IChannelWriter<T> writer;
        private AsyncLock writeLock;
        private PartitionStream writeTopic;
        private readonly FileCreationOptions fileOptions;
        private ChannelCursor cursor;

        internal PersistentChannelWriter(IChannelWriter<T> writer, bool singleWriter, long initialSize)
        {
            writeLock = singleWriter ? default : AsyncLock.Exclusive();
            this.writer = writer;
            fileOptions = new FileCreationOptions(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous | FileOptions.WriteThrough, initialSize);
            cursor = new ChannelCursor(writer.Location, StateFileName);
        }

        public long Position => cursor.Position;

        public override bool TryWrite(T item) => false;

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken token = default)
            => token.IsCancellationRequested ? new ValueTask<bool>(Task.FromCanceled<bool>(token)) : new ValueTask<bool>(true);

        private PartitionStream Partition => writer.GetOrCreatePartition(ref cursor, ref writeTopic, fileOptions, false);

        public override async ValueTask WriteAsync(T item, CancellationToken token)
        {
            using (await writeLock.Acquire(token).ConfigureAwait(false))
            {
                var lookup = Partition;
                await writer.SerializeAsync(item, lookup, token).ConfigureAwait(false);
                await lookup.FlushAsync(token).ConfigureAwait(false);
                cursor.Advance(lookup.Position);
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
}
