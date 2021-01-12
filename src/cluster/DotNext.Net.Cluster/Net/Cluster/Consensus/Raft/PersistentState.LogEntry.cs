using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using Text;

    public partial class PersistentState
    {
        /// <summary>
        /// Represents persistent log entry.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        protected readonly struct LogEntry : IRaftLogEntry, IAsyncBinaryReader
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

            /// <summary>
            /// Gets a value indicating that this entry is a snapshot entry.
            /// </summary>
            public bool IsSnapshot => SnapshotIndex.HasValue;

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length => metadata.Length;

            internal bool IsEmpty => metadata.Length == 0L;

            internal void Reset()
                => content.Adjust(metadata.Offset, Length);

            /// <summary>
            /// Reads the value of blittable type from the log entry
            /// and advances position in the underlying stream.
            /// </summary>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <typeparam name="T">The type of value to read.</typeparam>
            /// <returns>Decoded value of blittable type.</returns>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
            public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
                where T : unmanaged
                => content.ReadAsync<T>(buffer, token);

            private static async ValueTask ReadAsync(Stream input, Memory<byte> output, CancellationToken token)
            {
                if ((await input.ReadAsync(output, token).ConfigureAwait(false)) != output.Length)
                    throw new EndOfStreamException();
            }

            /// <summary>
            /// Reads the data of exact size.
            /// </summary>
            /// <param name="output">The buffer to be modified with the data from log entry.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing state of asynchronous execution.</returns>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
            public ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default)
                => ReadAsync(content, output, token);

            /// <summary>
            /// Reads the string of the specified encoding and length.
            /// </summary>
            /// <param name="length">The length of the string, in bytes.</param>
            /// <param name="context">The context of string decoding.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The decoded string.</returns>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
            public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
                => content.ReadStringAsync(length, context, buffer, token);

            /// <summary>
            /// Reads the string of the specified encoding.
            /// </summary>
            /// <param name="lengthFormat">Indicates how the string length is encoded in underlying stream.</param>
            /// <param name="context">The context of string decoding.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The decoded string.</returns>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
            public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default)
                => content.ReadStringAsync(lengthFormat, context, buffer, token);

            /// <summary>
            /// Copies the remaining content from this log entry to the specified stream.
            /// </summary>
            /// <param name="output">The stream used as copy destination.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing state of asynchronous execution.</returns>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            public Task CopyToAsync(Stream output, CancellationToken token = default)
                => content.CopyToAsync(output, token);

            /// <summary>
            /// Copies the remaining content from this log entry to the specified stream.
            /// </summary>
            /// <param name="output">The stream used as copy destination.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing state of asynchronous execution.</returns>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            public Task CopyToAsync(PipeWriter output, CancellationToken token = default)
                => content.CopyToAsync(output, token);

            /// <inheritdoc/>
            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                Reset();
                return new ValueTask(writer.CopyFromAsync(content, token));
            }

            /// <inheritdoc/>
            long? IDataTransferObject.Length => Length;

            /// <inheritdoc/>
            bool IDataTransferObject.IsReusable => false;

            /// <summary>
            /// Gets Raft term of this log entry.
            /// </summary>
            public long Term => metadata.Term;

            /// <summary>
            /// Gets timestamp of this log entry.
            /// </summary>
            public DateTimeOffset Timestamp => new DateTimeOffset(metadata.Timestamp, TimeSpan.Zero);

            /// <inheritdoc/>
            ValueTask<TResult> IDataTransferObject.GetObjectDataAsync<TResult, TDecoder>(TDecoder parser, CancellationToken token)
            {
                Reset();
                return IDataTransferObject.DecodeAsync<TResult, TDecoder>(content, parser, false, token);
            }
        }
    }
}
