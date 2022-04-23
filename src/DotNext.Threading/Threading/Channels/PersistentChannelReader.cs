using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Channels;

using IO;

internal sealed class PersistentChannelReader<T> : ChannelReader<T>, IChannelInfo, IDisposable
    where T : notnull
{
    private const string StateFileName = "reader.state";

    private interface IReadBuffer
    {
        bool TryRead([MaybeNullWhen(false)] out T result);

        void Add(T item);

        void Clear();
    }

    private sealed class SingleReaderBuffer : IReadBuffer
    {
        private AtomicBoolean readyToRead;
        [AllowNull]
        private T value;

        void IReadBuffer.Add(T item)
        {
            value = item;
            readyToRead.Value = true;
        }

        bool IReadBuffer.TryRead([MaybeNullWhen(false)] out T result)
        {
            bool success;
            result = (success = readyToRead.TrueToFalse())
                ? value
                : default;

            return success;
        }

        void IReadBuffer.Clear() => value = default;
    }

    private sealed class MultipleReadersBuffer : ConcurrentQueue<T>, IReadBuffer
    {
        void IReadBuffer.Add(T item) => Enqueue(item);

        bool IReadBuffer.TryRead([MaybeNullWhen(false)] out T result) => TryDequeue(out result);
    }

    private readonly IReadBuffer buffer;
    private readonly FileCreationOptions fileOptions;
    private readonly IChannelReader<T> reader;
    private readonly IncrementingEventCounter? readRate;
    private readonly bool reliableEnumeration;
    private AsyncLock readLock;
    private Partition? readTopic;
    private ChannelCursor cursor;

    internal PersistentChannelReader(IChannelReader<T> reader, bool singleReader, bool reliableEnumeration, IncrementingEventCounter? readRate)
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
        this.readRate = readRate;
        this.reliableEnumeration = reliableEnumeration;
    }

    public override Task Completion => reader.Completion;

    public long Position => cursor.Position;

    public override bool CanCount => true;

    public override int Count => checked((int)(reader.WrittenCount - Position));

    [MemberNotNull(nameof(readTopic))]
    private void GetOrCreatePartition() => reader.GetOrCreatePartition(ref cursor, ref readTopic, fileOptions, true);

    public override bool TryRead([MaybeNullWhen(false)] out T item)
    {
        var result = buffer.TryRead(out item);
        readRate?.Increment();
        return result;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public override async ValueTask<T> ReadAsync(CancellationToken token)
    {
        var task = await Task.WhenAny(reader.WaitToReadAsync(token), reader.Completion).ConfigureAwait(false);

        // propagate exception if needed
        await task.ConfigureAwait(false);

        // throw if completed
        if (ReferenceEquals(task, reader.Completion))
            throw new ChannelClosedException();

        // lock and deserialize
        T result;
        using (await readLock.AcquireAsync(token).ConfigureAwait(false))
        {
            GetOrCreatePartition();

            // reset file cache
            await readTopic.Stream.FlushAsync(token).ConfigureAwait(false);
            result = await reader.DeserializeAsync(readTopic, token).ConfigureAwait(false);
            await EndReadAsync(readTopic.Stream.Position, token).ConfigureAwait(false);
        }

        readRate?.Increment();
        return result;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public override async ValueTask<bool> WaitToReadAsync(CancellationToken token = default)
    {
        bool result;
        var task = await Task.WhenAny(reader.WaitToReadAsync(token), reader.Completion).ConfigureAwait(false);

        // propagate exception if needed
        await task.ConfigureAwait(false);

        if (ReferenceEquals(task, reader.Completion))
        {
            result = false;
        }
        else
        {
            // lock and deserialize
            using (await readLock.AcquireAsync(token).ConfigureAwait(false))
            {
                GetOrCreatePartition();

                // reset file cache
                await readTopic.Stream.FlushAsync(token).ConfigureAwait(false);
                buffer.Add(await reader.DeserializeAsync(readTopic, token).ConfigureAwait(false));
                await EndReadAsync(readTopic.Stream.Position, token).ConfigureAwait(false);
            }

            result = true;
        }

        return result;
    }

    private ValueTask EndReadAsync(long offset, CancellationToken token = default)
        => cursor.AdvanceAsync(offset, token);

    private void RollbackRead() => reader.RollbackRead();

    public override IAsyncEnumerable<T> ReadAllAsync(CancellationToken token = default)
        => reliableEnumeration ? new ReliableReader(this) { ProducerToken = token } : base.ReadAllAsync(token);

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            readTopic?.Dispose();
            readTopic = null;
            cursor.Dispose();
            buffer.Clear();
        }

        readLock.Dispose();
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PersistentChannelReader() => Dispose(false);

    private sealed class ReliableReader : IAsyncEnumerable<T>
    {
        private readonly PersistentChannelReader<T> reader;

        internal ReliableReader(PersistentChannelReader<T> reader)
        {
            Debug.Assert(reader is not null);

            this.reader = reader;
        }

        internal CancellationToken ProducerToken { private get; init; }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken consumerToken)
            => new ReliableAsyncEnumerator(reader, ProducerToken, consumerToken);
    }

    private sealed class ReliableAsyncEnumerator : Disposable, IAsyncEnumerator<T>
    {
        private readonly CancellationTokenSource? tokenSource;
        private readonly PersistentChannelReader<T> reader;
        private readonly CancellationToken token;
        private AsyncLock.Holder readLock;
        private long offset;
        private Optional<T> current;
        private bool rollbackRead;

        internal ReliableAsyncEnumerator(PersistentChannelReader<T> reader, CancellationToken producerToken, CancellationToken consumerToken)
        {
            Debug.Assert(reader is not null);

            this.reader = reader;
            tokenSource = producerToken.LinkTo(consumerToken);
            token = producerToken;
            offset = long.MinValue;
        }

        public T Current => current.Value;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        public async ValueTask<bool> MoveNextAsync()
        {
            // acquire lock if needed
            if (readLock.IsEmpty)
                readLock = await reader.readLock.AcquireAsync(token).ConfigureAwait(false);

            // commit previous read
            bool dryRun;
            if (offset < 0L)
            {
                dryRun = true;
            }
            else
            {
                await reader.EndReadAsync(offset).ConfigureAwait(false);
                reader.readRate?.Increment();
                dryRun = false;
            }

            bool result;
            rollbackRead = false;
            var task = await Task.WhenAny(reader.reader.WaitToReadAsync(token), reader.Completion).ConfigureAwait(false);

            // propagate exception if needed
            await task.ConfigureAwait(false);
            if (ReferenceEquals(task, reader.Completion))
            {
                result = false;
            }
            else
            {
                rollbackRead = true;
                reader.GetOrCreatePartition();

                if (dryRun)
                    reader.cursor.Adjust(reader.readTopic.Stream);

                // reset file cache
                var output = reader.readTopic.Stream;
                await output.FlushAsync(token).ConfigureAwait(false);
                current = await reader.reader.DeserializeAsync(reader.readTopic, token).ConfigureAwait(false);
                offset = output.Position;
                result = true;
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                readLock.Dispose();
                tokenSource?.Dispose();

                if (rollbackRead)
                    reader.RollbackRead();
            }

            base.Dispose(disposing);
        }

        ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();
    }
}