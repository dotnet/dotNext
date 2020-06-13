using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using IReadOnlySequence = Buffers.IReadOnlySequenceSource<byte>;
    using ReadOnlySequenceAccessor = MemoryMappedFiles.ReadOnlySequenceAccessor;

    public partial class FileBufferingWriter
    {
        private sealed class TailSegment : ReadOnlySequenceSegment<byte>
        {
            private static readonly Action<ReadOnlySequenceSegment<byte>, ReadOnlySequenceSegment<byte>> SegmentSetter =
                                DelegateHelpers.CreateOpenDelegate<TailSegment, ReadOnlySequenceSegment<byte>>(segm => segm.Next)
                                .Method
                                .CreateDelegate<Action<ReadOnlySequenceSegment<byte>, ReadOnlySequenceSegment<byte>>>();

            internal TailSegment(ReadOnlySequenceSegment<byte> previous, Memory<byte> memory)
            {
                Memory = memory;
                RunningIndex = previous.RunningIndex + previous.Memory.Length;
                SegmentSetter(previous, this);
            }
        }

        /// <summary>
        /// Represents source of <see cref="ReadOnlySequence{T}"/> that
        /// represents written content.
        /// </summary>
        public sealed class ReadOnlySequenceSource : Disposable, IReadOnlySequence
        {
            private readonly FileBufferingWriter writer;
            private readonly Memory<byte> tail;
            private readonly uint version;
            private ReadOnlySequenceAccessor? accessor;

            internal ReadOnlySequenceSource(FileBufferingWriter writer, int segmentLength)
            {
                var buffer = writer.buffer;
                tail = buffer.Memory.Slice(0, writer.position);
                accessor = writer.fileBackend is null ?
                    null :
                    new ReadOnlySequenceAccessor(CreateMemoryMappedFile(writer.fileBackend), segmentLength, writer.fileBackend.Length, false);
                this.writer = writer;
                version = ++writer.readVersion;
            }

            /// <summary>
            /// Obtains written content as a sequence of bytes.
            /// </summary>
            /// <value>The written content as a sequence of bytes.</value>
            /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
            public ReadOnlySequence<byte> Sequence
            {
                get
                {
                    ThrowIfDisposed();
                    if (accessor is null)
                        return new ReadOnlySequence<byte>(this.tail);

                    var (head, tail) = accessor.BuildSegments();
                    if (!this.tail.IsEmpty)
                        tail = new TailSegment(tail, this.tail);

                    return new ReadOnlySequence<byte>(head, 0, tail, tail.Memory.Length);
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    accessor?.Dispose();
                    accessor = null;
                }

                writer.ReleaseReadLock(version);
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Gets written content in the form of <see cref="ReadOnlySequence{T}"/> synchronously.
        /// </summary>
        /// <param name="segmentSize">The size of the contiguous segment of file to be mapped to memory.</param>
        /// <returns>The factory of <see cref="ReadOnlySequence{T}"/> instances.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="segmentSize"/> is less than or equal to zero.</exception>
        /// <exception cref="InvalidOperationException">The memory manager is already obtained but not disposed.</exception>
        public ReadOnlySequenceSource GetWrittenContent(int segmentSize)
        {
            if (segmentSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(segmentSize));
            if (IsReading)
                throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

            fileBackend?.Flush(true);
            return new ReadOnlySequenceSource(this, segmentSize);
        }

        /// <summary>
        /// Gets written content in the form of <see cref="ReadOnlySequence{T}"/> asynchronously.
        /// </summary>
        /// <param name="segmentSize">The size of the contiguous segment of file to be mapped to memory.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The factory of <see cref="ReadOnlySequence{T}"/> instances.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="segmentSize"/> is less than or equal to zero.</exception>
        public async ValueTask<ReadOnlySequenceSource> GetWrittenContentAsync(int segmentSize, CancellationToken token = default)
        {
            if (segmentSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(segmentSize));
            if (IsReading)
                throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

            if (fileBackend != null)
                await fileBackend.FlushAsync(token).ConfigureAwait(false);

            return new ReadOnlySequenceSource(this, segmentSize);
        }
    }
}