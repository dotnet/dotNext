#if !NETSTANDARD2_1
using System;
using System.Threading.Channels;

namespace DotNext.Collections.Concurrent
{
    using Threading.Channels;

    /// <summary>
    /// Represents various extensions for <see cref="RingBuffer{T}"/> class.
    /// </summary>
    public static class RingBuffer
    {
        /// <summary>
        /// Creates a reader for the buffer with asynchronous support.
        /// </summary>
        /// <remarks>
        /// The returned object can be shared between concurrent readers.
        /// This method is suitable in situations when producer is synchronous and write
        /// elements directly to <see cref="RingBuffer{T}"/>, because there is no way to inform
        /// reader that no more elements will be produced ever. To detach the reader from the buffer
        /// you can use <paramref name="cancellation"/> delegate and stop receiving new elements.
        /// </remarks>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        /// <param name="buffer">The ring buffer.</param>
        /// <param name="cancellation">The delegate that can be used to detach the reader from the buffer.</param>
        /// <returns>The reader of the buffer.</returns>
        public static ChannelReader<T> CreateReader<T>(this RingBuffer<T> buffer, out Action cancellation)
        {
            var reader = new RingBufferReader<T>(buffer);
            reader.Subscribe();
            cancellation = reader.Unsubscribe;
            return reader;
        }

        /// <summary>
        /// Creates a reader for the buffer with asynchronous support.
        /// </summary>
        /// <remarks>
        /// The returned object can be shared between concurrent readers.
        /// Usually, this method is used with paired asynchronous writer created
        /// with <see cref="CreateWriter{T}(RingBuffer{T})"/>.
        /// </remarks>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        /// <param name="buffer">The ring buffer.</param>
        /// <returns>The reader of the buffer.</returns>
        public static ChannelReader<T> CreateReader<T>(this RingBuffer<T> buffer)
        {
            var reader = new RingBufferReader<T>(buffer);
            reader.Subscribe();
            return reader;
        }

        /// <summary>
        /// Creates a writer for the buffer with asynchronous support.
        /// </summary>
        /// <remarks>
        /// If buffer has attached reader and writer both then the pipeline can be completed
        /// with <see cref="ChannelWriter{T}.TryComplete(Exception?)"/> method.
        /// </remarks>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        /// <param name="buffer">The ring buffer.</param>
        /// <returns>The writer for the buffer.</returns>
        public static ChannelWriter<T> CreateWriter<T>(this RingBuffer<T> buffer)
        {
            var writer = new RingBufferWriter<T>(buffer);
            writer.Subscribe();
            return writer;
        }
    }
}
#endif