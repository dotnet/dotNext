using System.Buffers;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;
using IReadOnlySequenceSource = Buffers.IReadOnlySequenceSource<byte>;

public partial class FileBufferingWriter
{
    private sealed class TailSegment : ReadOnlySequenceSegment<byte>
    {
        private static readonly Action<ReadOnlySequenceSegment<byte>, ReadOnlySequenceSegment<byte>> SegmentSetter;

        static TailSegment()
        {
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

    private sealed class MemoryManager : MemoryManager<byte>
    {
        internal readonly IMemorySegmentProvider Cursor;
        internal readonly Segment Segment;

        internal MemoryManager(IMemorySegmentProvider cursor, in Segment segment)
        {
            Cursor = cursor;
            Segment = segment;
        }

        public override Span<byte> GetSpan() => Cursor.GetSpan(in Segment);

        public override Memory<byte> Memory => CreateMemory(Segment.Length);

        public override MemoryHandle Pin(int elementIndex = 0) => Cursor.Pin(in Segment, elementIndex);

        public override void Unpin()
        {
            // nothing to do here
        }

        protected override void Dispose(bool disposing)
        {
            // nothing to do here
        }
    }

    private sealed class LazySegment : ReadOnlySequenceSegment<byte>, IDisposable
    {
        private readonly MemoryManager manager;

        private LazySegment(IMemorySegmentProvider cursor, in Segment segment)
        {
            manager = new(cursor, segment);
            Memory = manager.Memory;
        }

        private LazySegment(IMemorySegmentProvider cursor, int length)
            : this(cursor, new Segment(0L, length))
        {
        }

        private new LazySegment Next(int length)
        {
            var index = RunningIndex;
            var segment = new LazySegment(manager.Cursor, manager.Segment.Next(length))
            {
                RunningIndex = index + manager.Segment.Length,
            };
            base.Next = segment;
            return segment;
        }

        internal static void AddSegment(ReadOnlySequenceSource cursor, int length, ref LazySegment? first, ref LazySegment? last)
        {
            if (first is null || last is null)
                first = last = new(cursor, length) { RunningIndex = 0L };
            else
                last = last.Next(length);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                ((IDisposable)manager).Dispose();
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LazySegment() => Dispose(false);
    }

    /// <summary>
    /// The source of <see cref="ReadOnlySequence{T}"/> that
    /// represents written content.
    /// </summary>
    private sealed class ReadOnlySequenceSource : Disposable, IReadOnlySequenceSource, IMemorySegmentProvider
    {
        private readonly Memory<byte> tail;
        private readonly SafeFileHandle handle;
        private readonly int segmentLength;
        private MemoryOwner<byte> buffer;
        private Segment current;
        private ReadSession session;

        internal ReadOnlySequenceSource(FileBufferingWriter writer, int segmentLength)
        {
            Debug.Assert(writer.fileBackend is not null);

            var buffer = writer.buffer;
            tail = buffer.Memory.Slice(0, writer.position);
            handle = writer.fileBackend;
            this.buffer = writer.allocator.Invoke(segmentLength, true);
            this.segmentLength = segmentLength;

            session = writer.EnterReadMode(this);
        }

        private Span<byte> GetSpan(in Segment window)
        {
            var result = buffer.Span;

            if (current != window)
            {
                current = window;
                RandomAccess.Read(handle, result, window.Offset);
            }

            return result.Slice(0, window.Length);
        }

        Span<byte> IMemorySegmentProvider.GetSpan(in Segment window) => GetSpan(in window);

        MemoryHandle IMemorySegmentProvider.Pin(in Segment window, int elementIndex)
        {
            GetSpan(in window);
            return buffer.Memory.Pin();
        }

        private (ReadOnlySequenceSegment<byte>, ReadOnlySequenceSegment<byte>) BuildSegments()
        {
            LazySegment? first = null, last = null;
            for (var remainingLength = RandomAccess.GetLength(handle); remainingLength > 0;)
            {
                var segmentLength = (int)Math.Min(this.segmentLength, remainingLength);
                LazySegment.AddSegment(this, segmentLength, ref first, ref last);
                remainingLength -= segmentLength;
            }

            Debug.Assert(first is not null);
            Debug.Assert(last is not null);
            return (first, last);
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

                var (first, last) = BuildSegments();
                if (!tail.IsEmpty)
                    last = new TailSegment(last, tail);

                return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                buffer.Dispose();
                session.Dispose();
                session = default;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class BufferedMemorySource : Disposable, IReadOnlySequenceSource
    {
        private ReadOnlyMemory<byte> memory;
        private ReadSession session;

        internal BufferedMemorySource(FileBufferingWriter writer)
        {
            Debug.Assert(writer.fileBackend is null);

            var memory = writer.buffer;
            this.memory = memory.Memory.Slice(0, writer.position);
            session = writer.EnterReadMode(this);

            Debug.Assert(writer.IsReading);
        }

        public ReadOnlySequence<byte> Sequence => new(memory);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                session.Dispose();
                session = default;
                memory = default;
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

        return fileBackend is null ? new BufferedMemorySource(this) : new ReadOnlySequenceSource(this, segmentSize);
    }
}