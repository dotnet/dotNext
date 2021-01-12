using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.IO
{
    using IReadOnlySequenceSource = Buffers.IReadOnlySequenceSource<byte>;
    using ReadOnlySequenceAccessor = MemoryMappedFiles.ReadOnlySequenceAccessor;

    public partial class FileBufferingWriter
    {
        private sealed class TailSegment : ReadOnlySequenceSegment<byte>
        {
            private static readonly Action<ReadOnlySequenceSegment<byte>, ReadOnlySequenceSegment<byte>> SegmentSetter;

            static TailSegment()
            {
                // TODO: Should be replaced with function pointer in C# 9
                Ldnull();
                Ldftn(PropertySet(Type<ReadOnlySequenceSegment<byte>>(), nameof(Next)));
                Newobj(Constructor(Type<Action<ReadOnlySequenceSegment<byte>, ReadOnlySequenceSegment<byte>>>(), Type<object>(), Type<IntPtr>()));
                Pop(out SegmentSetter);
            }

            internal TailSegment(ReadOnlySequenceSegment<byte> previous, Memory<byte> memory)
            {
                Memory = memory;
                RunningIndex = previous.RunningIndex + previous.Memory.Length;
                SegmentSetter(previous, this);
            }
        }

        /// <summary>
        /// The source of <see cref="ReadOnlySequence{T}"/> that
        /// represents written content.
        /// </summary>
        private sealed class ReadOnlySequenceSource : Disposable, IReadOnlySequenceSource
        {
            private readonly Memory<byte> tail;
            private ReadOnlySequenceAccessor? accessor;
            private ReadSession session;

            internal ReadOnlySequenceSource(FileBufferingWriter writer, int segmentLength)
            {
                var buffer = writer.buffer;
                tail = buffer.Memory.Slice(0, writer.position);
                accessor = writer.fileBackend is null ?
                    null :
                    new ReadOnlySequenceAccessor(writer.fileBackend, segmentLength);
                session = writer.EnterReadMode(this);
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
                    session.Dispose();
                    session = default;
                }

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
        public IReadOnlySequenceSource GetWrittenContent(int segmentSize)
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
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<IReadOnlySequenceSource> GetWrittenContentAsync(int segmentSize, CancellationToken token = default)
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