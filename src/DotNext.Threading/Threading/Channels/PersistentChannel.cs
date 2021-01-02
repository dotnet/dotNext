using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    /// <summary>
    /// Represents persistent unbounded channel.
    /// </summary>
    /// <typeparam name="TInput">Specifies the type of data that may be written to the channel.</typeparam>
    /// <typeparam name="TOutput">Specifies the type of data that may be read from the channel.</typeparam>
    public abstract class PersistentChannel<TInput, TOutput> : Channel<TInput, TOutput>, IChannelWriter<TInput>, IChannelReader<TOutput>, IDisposable
        where TInput : notnull
        where TOutput : notnull
    {
        private readonly int maxCount;
        private readonly IAsyncEvent readTrigger;
        private readonly int bufferSize;
        private readonly DirectoryInfo location;
        private readonly IncrementingEventCounter? writeRate;

        /// <summary>
        /// Initializes a new persistent channel with the specified options.
        /// </summary>
        /// <param name="options">The options of the channel.</param>
        protected PersistentChannel(PersistentChannelOptions options)
        {
            maxCount = options.PartitionCapacity;
            bufferSize = options.BufferSize;
            location = new DirectoryInfo(options.Location);
            if (!location.Exists)
                location.Create();
            var writer = new PersistentChannelWriter<TInput>(this, options.SingleWriter, options.InitialPartitionSize);
            var reader = new PersistentChannelReader<TOutput>(this, options.SingleReader, options.ReadRateCounter);
            Reader = reader;
            Writer = writer;
            readTrigger = new AsyncCounter(writer.Position - reader.Position);
            writeRate = options.WriteRateCounter;
        }

        /// <summary>
        /// Gets ratio between number of consumed and produced messages.
        /// </summary>
        public double Throughput
        {
            get
            {
                double result = ((Reader as IChannelInfo)?.Position ?? 0D) / ((Writer as IChannelInfo)?.Position ?? 0D);
                return double.IsNaN(result) ? 1D : result;
            }
        }

        /// <summary>
        /// Gets the number of unread messages.
        /// </summary>
        /// <value>The number of unread messages.</value>
        public long RemainingCount => ((Writer as IChannelInfo)?.Position ?? 0L) - ((Reader as IChannelInfo)?.Position ?? 0L);

        /// <inheritdoc />
        long IChannelReader<TOutput>.WrittenCount => (Writer as IChannelInfo)?.Position ?? 0L;

        /// <inheritdoc />
        DirectoryInfo IChannel.Location => location;

        /// <inheritdoc />
        void IChannelWriter<TInput>.MessageReady()
        {
            readTrigger.Signal();
            writeRate?.Increment();
        }

        /// <inheritdoc />
        ValueTask IChannelWriter<TInput>.SerializeAsync(TInput input, PartitionStream output, CancellationToken token)
            => SerializeAsync(input, output, token);

        /// <inheritdoc />
        Task IChannelReader<TOutput>.WaitToReadAsync(CancellationToken token) => readTrigger.WaitAsync(token);

        private PartitionStream CreateTopicStream(long partition, in FileCreationOptions options)
        {
            var result = new PartitionStream(location, partition, options, bufferSize);
            if (result.Length == 0L && options.InitialSize > 0L)
                result.SetLength(options.InitialSize);
            return result;
        }

        /// <inheritdoc />
        PartitionStream IChannel.GetOrCreatePartition(ref ChannelCursor state, [NotNull]ref PartitionStream? partition, in FileCreationOptions options, bool deleteOnDispose)
        {
            var partitionNumber = state.Position / maxCount;
            PartitionStream result;
            if (partition is null)
            {
                partition = result = CreateTopicStream(partitionNumber, options);
                state.Adjust(result);
            }
            else if (partition.PartitionNumber == partitionNumber)
            {
                result = partition;
            }
            else
            {
                // delete previous topic file
                var fileName = partition.Name;
                partition.Dispose();
                if (deleteOnDispose)
                    File.Delete(fileName);
                partition = result = CreateTopicStream(partitionNumber, options);
                state.Reset();
            }

            return result;
        }

        /// <summary>
        /// Serializes message to stream asynchronously.
        /// </summary>
        /// <param name="input">The message to serialize.</param>
        /// <param name="output">The stream used to serialize object.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of operation.</returns>
        protected abstract ValueTask SerializeAsync(TInput input, Stream output, CancellationToken token);

        /// <summary>
        /// Deserializes message from stream asynchronously.
        /// </summary>
        /// <param name="input">The stream containing serialized message.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Deserialized message.</returns>
        protected abstract ValueTask<TOutput> DeserializeAsync(Stream input, CancellationToken token);

        /// <inheritdoc />
        ValueTask<TOutput> IChannelReader<TOutput>.DeserializeAsync(PartitionStream input, CancellationToken token)
            => DeserializeAsync(input, token);

        /// <summary>
        /// Releases managed and, optionally, unmanaged resources associated with this channel.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to dispose all resources; <see langword="false"/> to release unmanaged resources only.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                readTrigger.Dispose();
                (Reader as IDisposable)?.Dispose();
                (Writer as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Releases all resources associated with this channel.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases file handles associated with this channel.
        /// </summary>
        ~PersistentChannel() => Dispose(false);
    }
}
