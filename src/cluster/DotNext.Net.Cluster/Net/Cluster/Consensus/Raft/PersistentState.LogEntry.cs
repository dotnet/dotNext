using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using IO.Log;
    using Text;

    public partial class PersistentState
    {
        /// <summary>
        /// Represents persistent log entry.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        protected readonly struct LogEntry : IRaftLogEntry
        {
            private readonly StreamSegment content;
            private readonly LogEntryMetadata metadata;
            private readonly Memory<byte> buffer;
            internal readonly long? SnapshotIndex;

            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in LogEntryMetadata metadata)
            {
                this.metadata = metadata;
                content = cachedContent;
                buffer = sharedBuffer;
                SnapshotIndex = null;
            }

            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in SnapshotMetadata metadata)
            {
                this.metadata = metadata.RecordMetadata;
                content = cachedContent;
                buffer = sharedBuffer;
                SnapshotIndex = metadata.Index;
            }

            bool ILogEntry.IsSnapshot => SnapshotIndex.HasValue;

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length => metadata.Length;

            internal Stream AdjustPosition()
            {
                content.Adjust(metadata.Offset, Length);
                return content;
            }

            /// <summary>
            /// Reads the number of bytes using the pre-allocated buffer.
            /// </summary>
            /// <remarks>
            /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
            /// </remarks>
            /// <param name="count">The number of bytes to read.</param>
            /// <returns>The span of bytes representing buffer segment.</returns>
            /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
            public ReadOnlySpan<byte> Read(int count)
            {
                var result = buffer.Slice(0, count).Span;
                return content.Read(result) == count ? result : throw new EndOfStreamException();
            }

            /// <summary>
            /// Reads asynchronously the number of bytes using the pre-allocated buffer.
            /// </summary>
            /// <remarks>
            /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
            /// </remarks>
            /// <param name="count">The number of bytes to read.</param>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <returns>The span of bytes representing buffer segment.</returns>
            /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
            public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(int count, CancellationToken token = default)
            {
                var result = buffer.Slice(0, count);
                return await content.ReadAsync(result, token).ConfigureAwait(false) == count ? result : throw new EndOfStreamException();
            }

            /// <summary>
            /// Reads the string using the specified encoding.
            /// </summary>
            /// <remarks>
            /// The characters should be prefixed with the length in the underlying stream.
            /// </remarks>
            /// <param name="length">The length of the string, in bytes.</param>
            /// <param name="context">The decoding context.</param>
            /// <returns>The string decoded from the log entry content stream.</returns>
            public string ReadString(int length, in DecodingContext context) => content.ReadString(length, context, buffer.Span);

            /// <summary>
            /// Reads the string asynchronously using the specified encoding.
            /// </summary>
            /// <remarks>
            /// The characters should be prefixed with the length in the underlying stream.
            /// </remarks>
            /// <param name="length">The length of the string, in bytes.</param>
            /// <param name="context">The decoding context.</param>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <returns>The string decoded from the log entry content stream.</returns>
            public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
                => content.ReadStringAsync(length, context, buffer, token);

            /// <summary>
            /// Copies the object content into the specified stream.
            /// </summary>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <param name="output">The output stream receiving object content.</param>
            public async ValueTask CopyToAsync(Stream output, CancellationToken token) => await AdjustPosition().CopyToAsync(output, buffer, token).ConfigureAwait(false);

            /// <summary>
            /// Copies the object content into the specified stream synchronously.
            /// </summary>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <param name="output">The output stream receiving object content.</param>
            public void CopyTo(Stream output, CancellationToken token) => AdjustPosition().CopyTo(output, buffer.Span, token);

            /// <summary>
            /// Copies the log entry content into the specified pipe writer.
            /// </summary>
            /// <param name="output">The writer.</param>
            /// <param name="token">The token that can be used to cancel operation.</param>
            /// <returns>The task representing asynchronous execution of this method.</returns>
            public ValueTask CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask(AdjustPosition().CopyToAsync(output, token));

            long? IDataTransferObject.Length => Length;
            bool IDataTransferObject.IsReusable => false;

            /// <summary>
            /// Gets Raft term of this log entry.
            /// </summary>
            public long Term => metadata.Term;

            /// <summary>
            /// Gets timestamp of this log entry.
            /// </summary>
            public DateTimeOffset Timestamp => new DateTimeOffset(metadata.Timestamp, TimeSpan.Zero);
        }
    }
}
