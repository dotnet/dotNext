using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    internal sealed class PersistentChannelReader<T> : ChannelReader<T>, IChannelInfo, IDisposable
    {
        private const string StateFileName = "reader.state";
        private interface IReadBuffer
        {
            bool TryRead(out T result);

            void Add(T item);

            //TODO: Clear can be implemented easily in .NET Standard 2.1
            //void Clear();
        }

        private sealed class SingleReaderBuffer : IReadBuffer
        {
            private AtomicBoolean readyToRead;
            private T value;

            void IReadBuffer.Add(T item)
            {
                value = item;
                readyToRead.Value = true;
            }

            bool IReadBuffer.TryRead(out T result)
            {
                if (readyToRead.CompareAndSet(true, false))
                {
                    result = value;
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }
        }

        private sealed class MultipleReadersBuffer : ConcurrentQueue<T>, IReadBuffer
        {
            void IReadBuffer.Add(T item) => Enqueue(item);

            bool IReadBuffer.TryRead(out T result) => TryDequeue(out result);
        }

        private readonly IChannelReader<T> reader;
        private AsyncLock readLock;
        private PartitionStream readTopic;
        private readonly IReadBuffer buffer;
        private readonly FileCreationOptions fileOptions;
        private ChannelCursor cursor;

        internal PersistentChannelReader(IChannelReader<T> reader, bool singleReader)
        {
            this.reader = reader;
            if (singleReader)
            {
                readLock = default;
                buffer = new SingleReaderBuffer();
            }
            else
            {
                readLock = AsyncLock.Exclusive();
                buffer = new MultipleReadersBuffer();
            }
            fileOptions = new FileCreationOptions(FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.Asynchronous | FileOptions.SequentialScan);
            cursor = new ChannelCursor(reader.Location, StateFileName);
        }

        public long Position => cursor.Position;

        private PartitionStream Partition => reader.GetOrCreatePartition(ref cursor, ref readTopic, fileOptions, true);

        public override bool TryRead(out T item) => buffer.TryRead(out item);

        public override async ValueTask<T> ReadAsync(CancellationToken token)
        {
            await reader.WaitToReadAsync(token).ConfigureAwait(false);
            //lock and deserialize
            T result;
            using (await readLock.Acquire(token).ConfigureAwait(false))
            {
                var lookup = Partition;
                //reset file cache
                await lookup.FlushAsync(token).ConfigureAwait(false);
                result = await reader.DeserializeAsync(lookup, token).ConfigureAwait(false);
                cursor.Advance(lookup.Position);
            }
            return result;
        }

        public override async ValueTask<bool> WaitToReadAsync(CancellationToken token = default)
        {
            await reader.WaitToReadAsync(token).ConfigureAwait(false);
            //lock and deserialize
            using (await readLock.Acquire(token).ConfigureAwait(false))
            {
                var lookup = Partition;
                buffer.Add(await reader.DeserializeAsync(lookup, token).ConfigureAwait(false));
                cursor.Advance(lookup.Position);
            }
            return true;
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                readTopic?.Dispose();
                readTopic = null;
                cursor.Dispose();
            }
            readLock.Dispose();
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PersistentChannelReader() => Dispose(false);
    }
}
