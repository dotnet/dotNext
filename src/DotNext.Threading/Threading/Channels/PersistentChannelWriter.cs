using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    internal sealed class PersistentChannelWriter<T> : ChannelWriter<T>, IChannelHandler, IDisposable
    {
        private const string StateFileName = "writer.state";
        private readonly IChannelWriter<T> writer;
        private AsyncLock writeLock;
        private TopicStream writeTopic;
        private volatile bool closed;
        private readonly FileCreationOptions fileOptions;
        private State state;

        internal PersistentChannelWriter(IChannelWriter<T> writer, bool singleWriter)
        {
            writeLock = singleWriter ? default : AsyncLock.Exclusive();
            this.writer = writer;
            fileOptions = new FileCreationOptions(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            state = new State(writer.Location, StateFileName);
        }

        public long Position => state.Position;

        public override bool TryWrite(T item) => false;

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken token = default)
            => token.IsCancellationRequested ? new ValueTask<bool>(Task.FromCanceled<bool>(token)) : new ValueTask<bool>(!closed);

        private TopicStream NextTopic()
            => writer.GetOrCreateTopic(ref state, ref writeTopic, fileOptions, false);

        public override async ValueTask WriteAsync(T item, CancellationToken token)
        {
            if (closed)
                throw new ChannelClosedException();
            using (await writeLock.Acquire(token).ConfigureAwait(false))
            {
                await writer.SerializeAsync(item, NextTopic(), token).ConfigureAwait(false);
                writer.MessageReady();
            }
            state.Advance();
        }

        public override bool TryComplete(Exception error = null)
        {
            var result = writer.TryComplete(error);
            if (result)
                closed = true;
            return result;
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                writeTopic?.Dispose();
                writeTopic = null;
                state.Dispose();
            }
            writeLock.Dispose();
            closed = true;
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PersistentChannelWriter() => Dispose(false);
    }
}
