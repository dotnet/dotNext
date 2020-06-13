using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace DotNext.IO.MemoryMappedFiles
{
    using IReadOnlySequenceSource = Buffers.IReadOnlySequenceSource<byte>;

    /// <summary>
    /// Represents factory of <see cref="ReadOnlySequence{T}"/> objects
    /// representing memory-mapped file content.
    /// </summary>
    /// <remarks>
    /// A sequence returned by <see cref="Sequence"/> property should not be
    /// accessed concurrently. For concurrent access, you need to obtain a new instance of
    /// <see cref="ReadOnlySequenceAccessor"/> class.
    /// The class uses lazy initialization of memory-mapped file segment
    /// every time when <see cref="ReadOnlySequence{T}"/> switching between segments.
    /// </remarks>
    public sealed class ReadOnlySequenceAccessor : Disposable, IReadOnlySequenceSource, IConvertible<ReadOnlySequence<byte>>
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Segment : IEquatable<Segment>
        {
            internal readonly int Length;
            internal readonly long Offset;

            internal Segment(long offset, int length)
            {
                Length = length;
                Offset = offset;
            }

            internal Segment Next(int length) => new Segment(Length + Offset, length);

            public bool Equals(Segment other)
                => Length == other.Length && Offset == other.Offset;

            public override bool Equals(object other) => other is Segment window && Equals(window);

            public override int GetHashCode()
                => HashCode.Combine(Offset, Length);

            public static bool operator ==(in Segment x, in Segment y)
                => x.Length == y.Length && x.Offset == y.Offset;

            public static bool operator !=(in Segment x, in Segment y)
                => x.Length != y.Length || x.Offset != y.Offset;
        }

        private sealed class MemoryManager : MemoryManager<byte>
        {
            internal readonly ReadOnlySequenceAccessor Cursor;
            internal readonly Segment Segment;

            internal MemoryManager(ReadOnlySequenceAccessor cursor, Segment segment)
            {
                Cursor = cursor;
                Segment = segment;
            }

            public override unsafe Span<byte> GetSpan()
                => new Span<byte>(Cursor.GetMemory(Segment), Segment.Length);

            public override Memory<byte> Memory => CreateMemory(Segment.Length);

            public override unsafe MemoryHandle Pin(int index)
                => new MemoryHandle(Cursor.GetMemory(Segment) + index);

            public override void Unpin()
            {
            }

            protected override void Dispose(bool disposing)
            {
            }
        }

        private sealed class MappedSegment : ReadOnlySequenceSegment<byte>, IDisposable
        {
            private readonly MemoryManager manager;

            private MappedSegment(ReadOnlySequenceAccessor cursor, Segment segment)
            {
                manager = new MemoryManager(cursor, segment);
                Memory = manager.Memory;
            }

            private MappedSegment(ReadOnlySequenceAccessor cursor, int length)
                : this(cursor, new Segment(0L, length))
            {
            }

            private new MappedSegment Next(int length)
            {
                var index = RunningIndex;
                var segment = new MappedSegment(manager.Cursor, manager.Segment.Next(length))
                {
                    RunningIndex = index + manager.Segment.Length,
                };
                base.Next = segment;
                return segment;
            }

            internal static void AddSegment(ReadOnlySequenceAccessor cursor, int length, ref MappedSegment? first, ref MappedSegment? last)
            {
                if (first is null || last is null)
                    first = last = new MappedSegment(cursor, length) { RunningIndex = 0L };
                else
                    last = last.Next(length);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    manager.As<IDisposable>().Dispose();
                }
            }

            void IDisposable.Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~MappedSegment() => Dispose(false);
        }

        private readonly int segmentLength;
        private readonly long totalLength;
        private readonly MemoryMappedFile mappedFile;
        private readonly bool ownsFile;
        private Segment current;
        private MemoryMappedViewAccessor? segment;
        private unsafe byte* ptr;

        internal ReadOnlySequenceAccessor(MemoryMappedFile file, int segmentLength, long totalLength, bool leaveOpen = true)
        {
            mappedFile = file;
            this.segmentLength = segmentLength;
            this.totalLength = totalLength;
            ownsFile = !leaveOpen;
        }

        internal (ReadOnlySequenceSegment<byte> Head, ReadOnlySequenceSegment<byte> Tail) BuildSegments()
        {
            MappedSegment? first = null, last = null;
            for (var remainingLength = totalLength; remainingLength > 0; )
            {
                var segmentLength = (int)Math.Min(this.segmentLength, remainingLength);
                MappedSegment.AddSegment(this, segmentLength, ref first, ref last);
                remainingLength -= segmentLength;
            }

            Debug.Assert(first != null);
            Debug.Assert(last != null);
            return (first, last);
        }

        /// <summary>
        /// Gets the sequence of memory-mapped file fragments.
        /// </summary>
        /// <remarks>
        /// The sequence produced by this instance should not be accessed
        /// concurrently.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public ReadOnlySequence<byte> Sequence
        {
            get
            {
                ThrowIfDisposed();
                if (totalLength == 0)
                    return ReadOnlySequence<byte>.Empty;

                var (head, tail) = BuildSegments();
                return new ReadOnlySequence<byte>(head, 0, tail, tail.Memory.Length);
            }
        }

        /// <inheritdoc/>
        ReadOnlySequence<byte> IConvertible<ReadOnlySequence<byte>>.Convert()
            => Sequence;

        private unsafe byte* GetMemory(in Segment window)
        {
            ThrowIfDisposed();
            if (current != window)
            {
                segment?.ReleasePointerAndDispose();
                ptr = default;
                current = window;
                segment = mappedFile.CreateViewAccessor(window.Offset, window.Length, MemoryMappedFileAccess.Read);
                segment.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            }

            Debug.Assert(segment != null);
            return ptr + segment.PointerOffset;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                segment?.ReleasePointerAndDispose();
                segment = null;
                if (ownsFile)
                    mappedFile.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}