using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO.MemoryMappedFiles;

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
public sealed class ReadOnlySequenceAccessor : Disposable, IReadOnlySequenceSource, IMemorySegmentProvider
{
    private sealed class MemoryManager : MemoryManager<byte>
    {
        internal readonly IMemorySegmentProvider Cursor;
        internal readonly Segment Segment;

        internal MemoryManager(IMemorySegmentProvider cursor, in Segment segment)
        {
            Cursor = cursor;
            Segment = segment;
        }

        public override unsafe Span<byte> GetSpan()
            => Cursor.GetSpan(in Segment);

        public override Memory<byte> Memory => CreateMemory(Segment.Length);

        public override unsafe MemoryHandle Pin(int index)
            => Cursor.Pin(in Segment, index);

        public override void Unpin()
        {
            // nothing to do here
        }

        protected override void Dispose(bool disposing)
        {
            // nothing to do here
        }
    }

    private sealed class MappedSegment : ReadOnlySequenceSegment<byte>, IDisposable
    {
        private readonly MemoryManager manager;

        private MappedSegment(IMemorySegmentProvider cursor, in Segment segment)
        {
            manager = new(cursor, segment);
            Memory = manager.Memory;
        }

        private MappedSegment(ReadOnlySequenceAccessor cursor, int length)
            : this(cursor, new Segment { Length = length })
        {
        }

        private new MappedSegment Next(int length)
        {
            var index = RunningIndex;
            var segment = new MappedSegment(manager.Cursor, manager.Segment >> length)
            {
                RunningIndex = index + manager.Segment.Length,
            };
            base.Next = segment;
            return segment;
        }

        internal static void AddSegment(ReadOnlySequenceAccessor cursor, int length, ref MappedSegment? first, ref MappedSegment? last)
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

        ~MappedSegment() => Dispose(false);
    }

    private readonly int segmentLength;
    private readonly long totalLength;
    private readonly MemoryMappedFile mappedFile;
    private readonly bool ownsFile;
    private Segment current;
    private MemoryMappedViewAccessor? segment;
    private unsafe byte* ptr;

    private ReadOnlySequenceAccessor(MemoryMappedFile file, int segmentSize, long size, bool leaveOpen)
    {
        if (segmentSize <= 0 || segmentSize > size)
            throw new ArgumentOutOfRangeException(nameof(segmentSize));
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        mappedFile = file;
        segmentLength = segmentSize;
        totalLength = size;
        ownsFile = !leaveOpen;
    }

    /// <summary>
    /// Initializes a new accessor over memory-mapped file segments represented
    /// as <see cref="System.Buffers.ReadOnlySequence{T}"/>.
    /// </summary>
    /// <param name="file">The memory-mapped file.</param>
    /// <param name="segmentSize">
    /// The size of single segment, in bytes, that can be returned by <see cref="System.Buffers.ReadOnlySequence{T}"/>
    /// as contiguous block of memory. So this parameter defines actual amount of occupied virtual memory.
    /// </param>
    /// <param name="size">The observable length, in bytes, of memory-mapped file.</param>
    /// <returns>The object providing access to memory-mapped file via <see cref="System.Buffers.ReadOnlySequence{T}"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="segmentSize"/> is less than or equal to zero;
    /// or <paramref name="size"/> is less than or equal to zero;
    /// or <paramref name="segmentSize"/> is greater than <paramref name="size"/>.
    /// </exception>
    public ReadOnlySequenceAccessor(MemoryMappedFile file, int segmentSize, long size)
        : this(file, segmentSize, size, true)
    {
    }

    /// <summary>
    /// Initializes a new accessor over memory-mapped file segments represented
    /// as <see cref="System.Buffers.ReadOnlySequence{T}"/>.
    /// </summary>
    /// <param name="file">The file stream.</param>
    /// <param name="segmentSize">
    /// The size of single segment, in bytes, that can be returned by <see cref="System.Buffers.ReadOnlySequence{T}"/>
    /// as contiguous block of memory. So this parameter defines actual amount of occupied virtual memory.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="segmentSize"/> is less than or equal to zero; or it's greater than file size.</exception>
    public ReadOnlySequenceAccessor(FileStream file, int segmentSize)
        : this(MemoryMappedFile.CreateFromFile(file, null, file.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, true), segmentSize, file.Length, false)
    {
    }

    private (ReadOnlySequenceSegment<byte> Head, ReadOnlySequenceSegment<byte> Tail) BuildSegments()
    {
        MappedSegment? first = null, last = null;
        for (var remainingLength = totalLength; remainingLength > 0;)
        {
            var segmentLength = (int)Math.Min(this.segmentLength, remainingLength);
            MappedSegment.AddSegment(this, segmentLength, ref first, ref last);
            remainingLength -= segmentLength;
        }

        Debug.Assert(first is not null);
        Debug.Assert(last is not null);
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

    private unsafe Span<byte> GetSpan(in Segment window)
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

        Debug.Assert(segment is not null);
        return new(ptr + segment.PointerOffset, window.Length);
    }

    /// <inheritdoc />
    unsafe Span<byte> IMemorySegmentProvider.GetSpan(in Segment window) => GetSpan(in window);

    /// <inheritdoc />
    unsafe MemoryHandle IMemorySegmentProvider.Pin(in Segment window, int elementIndex)
        => new(Unsafe.AsPointer(ref GetSpan(in window)[elementIndex]));

    /// <inheritdoc/>
    protected override unsafe void Dispose(bool disposing)
    {
        if (disposing)
        {
            segment?.ReleasePointerAndDispose();
            segment = null;
            if (ownsFile)
                mappedFile.Dispose();
            current = default;
        }

        ptr = null;
        base.Dispose(disposing);
    }
}