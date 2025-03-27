using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Number = Numerics.Number;
using List = Collections.Generic.List;

/// <summary>
/// Represents a pool of segments of the limited size on the disk.
/// </summary>
/// <remarks>
/// This class can be used to organize on-disk cache in combination with <c>RandomAccessCache</c>.
/// All members of this class are thread-safe.
/// </remarks>
[DebuggerDisplay($"FreeList = {{{nameof(ReturnedSegments)}}}")]
public partial class DiskSpacePool : Disposable
{
    /// <summary>
    /// Initializes a new pool of segments on the disk.
    /// </summary>
    /// <param name="path">The path to the file that is used internally to allocate the disk space.</param>
    /// <param name="maxSegmentSize">The maximum size of the segment to rent, in bytes.</param>
    /// <param name="options">The configuration options.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSegmentSize"/> is negative or zero.</exception>
    public DiskSpacePool(string path, int maxSegmentSize, in Options options = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSegmentSize);

        long preallocationSize;
        if (options.OptimizedDiskAllocation)
        {
            var pageSize = Environment.SystemPageSize;
            maxSegmentSize = (int)Number.RoundUp((uint)maxSegmentSize, (uint)pageSize);
            zeroes = AllocateZeroedBuffer(maxSegmentSize, pageSize);
            preallocationSize = 0L;
        }
        else
        {
            preallocationSize = options.ExpectedNumberOfSegments * (long)maxSegmentSize;
            zeroes = [];
        }

        handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, options.FileOptions, preallocationSize);
        File.SetAttributes(handle, options.FileAttributes);
        MaxSegmentSize = maxSegmentSize;
        cursor = -maxSegmentSize;

        const string entryPointName = "posix_fadvise";
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD()) &&
            NativeLibrary.TryGetExport(NativeLibrary.GetMainProgramHandle(), entryPointName, out var address))
        {
            unsafe
            {
                var posix_fadvise = (delegate*unmanaged<nint, long, long, int, int>)address;

                const int POSIX_FADV_NOREUSE = 5;
                var errorCode = posix_fadvise(handle.DangerousGetHandle(), 0L, 0L, POSIX_FADV_NOREUSE);
                Debug.Assert(errorCode is 0);
            }
        }

        static IReadOnlyList<ReadOnlyMemory<byte>> AllocateZeroedBuffer(int segmentSize, int pageSize)
        {
            Debug.Assert(segmentSize % pageSize is 0);
            
            Memory<byte> buffer;
            if (OperatingSystem.IsWindows())
            {
                // for efficient scatter/gather on Windows, the buffer needs to be page aligned
                buffer = GC.AllocateUninitializedArray<byte>(pageSize * 2, pinned: true);

                var address = (nuint)Intrinsics.AddressOf(in buffer.Span[0]); // pinned already
                var remainder = (int)(address % (uint)pageSize);

                buffer = buffer.Slice(remainder is 0 ? 0 : pageSize - remainder, pageSize);
                Debug.Assert(Intrinsics.AddressOf(in buffer.Span[0]) % pageSize is 0);
            
                buffer.Span.Clear();
            }
            else
            {
                // matches Linux's UIO_FASTIOV, which is the number of 'struct iovec' that get stackalloced in the Linux kernel
                const int ioVecStackallocThreshold = 8;

                var bufferSize = segmentSize / ioVecStackallocThreshold;
                buffer = GC.AllocateArray<byte>(bufferSize < pageSize ? segmentSize : bufferSize, pinned: true);
            }

            return List.Repeat<ReadOnlyMemory<byte>>(buffer, segmentSize / buffer.Length);
        }
    }

    /// <summary>
    /// Initializes a new pool of segments on the disk in temporary directory.
    /// </summary>
    /// <param name="maxSegmentSize">The maximum size of the segment to rent, in bytes.</param>
    /// <param name="options">The configuration options.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSegmentSize"/> is negative or zero.</exception>
    public DiskSpacePool(int maxSegmentSize, in Options options = default)
        : this(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), maxSegmentSize, in options)
    {
    }

    /// <summary>
    /// Gets the maximum size of the segment to be rented, in bytes.
    /// </summary>
    public int MaxSegmentSize { get; }

    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int ReturnedSegments
    {
        get
        {
            var result = 0;
            for (var node = freeList; node is not null; node = node.Next)
                result++;

            return result;
        }
    }

    /// <summary>
    /// Rents a space on the disk.
    /// </summary>
    /// <returns>The segment within the file.</returns>
    public Segment Rent()
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        return new(this, RentOffset());
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            handle.Dispose();
            freeList = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Provides the random access to the data within the segment.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Segment : IDisposable, IAsyncDisposable
    {
        private readonly WeakReference<DiskSpacePool?> poolRef;
        private readonly long absoluteOffset;

        internal Segment(DiskSpacePool pool, long offset)
        {
            poolRef = new(pool, trackResurrection: false);
            absoluteOffset = offset;
        }

        /// <summary>
        /// Writes to the segment at the specified offset.
        /// </summary>
        /// <param name="buffer">The data to be written.</param>
        /// <param name="offset">The offset within the segment.</param>
        /// <exception cref="ObjectDisposedException">The segment has been returned to the pool.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative; or <paramref name="buffer"/> is too large.</exception>
        public void Write(ReadOnlySpan<byte> buffer, int offset = 0)
        {
            ObjectDisposedException.ThrowIf(!poolRef.TryGetTarget(out var pool), this);

            if (offset < 0 || (uint)(offset + buffer.Length) > (uint)pool.MaxSegmentSize)
                throw new ArgumentOutOfRangeException(nameof(offset));

            pool.Write(absoluteOffset, buffer, offset);
        }

        /// <summary>
        /// Writes to the segment at the specified offset.
        /// </summary>
        /// <param name="buffer">The data to be written.</param>
        /// <param name="offset">The offset within the segment.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">The segment has been returned to the pool.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative; or <paramref name="buffer"/> is too large.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, int offset = 0, CancellationToken token = default)
        {
            ValueTask task;
            if (!poolRef.TryGetTarget(out var pool))
            {
                task = ValueTask.FromException(new ObjectDisposedException(nameof(Segment)));
            }
            else if (offset < 0 || (uint)(offset + buffer.Length) > (uint)pool.MaxSegmentSize)
            {
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(offset)));
            }
            else
            {
                task = pool.WriteAsync(absoluteOffset, buffer, offset, token);
            }

            return task;
        }

        /// <summary>
        /// Reads the data from the segment at the specified offset.
        /// </summary>
        /// <param name="buffer">The buffer to be modified with the data from the segment.</param>
        /// <param name="offset">The offset within the segment.</param>
        /// <returns>The number of bytes written to <paramref name="buffer"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative or larger than <see cref="DiskSpacePool.MaxSegmentSize"/>.</exception>
        /// <exception cref="ObjectDisposedException">The segment has been returned to the pool.</exception>
        public int Read(Span<byte> buffer, int offset = 0)
        {
            ObjectDisposedException.ThrowIf(!poolRef.TryGetTarget(out var pool), this);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)pool.MaxSegmentSize, nameof(offset));

            return pool.Read(absoluteOffset, buffer, offset);
        }

        /// <summary>
        /// Reads the data from the segment at the specified offset.
        /// </summary>
        /// <param name="buffer">The buffer to be modified with the data from the segment.</param>
        /// <param name="offset">The offset within the segment.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The number of bytes written to <paramref name="buffer"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative or larger than <see cref="DiskSpacePool.MaxSegmentSize"/>.</exception>
        /// <exception cref="ObjectDisposedException">The segment has been returned to the pool.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask<int> ReadAsync(Memory<byte> buffer, int offset = 0, CancellationToken token = default)
        {
            ValueTask<int> task;
            if (!poolRef.TryGetTarget(out var pool))
            {
                task = ValueTask.FromException<int>(new ObjectDisposedException(nameof(Segment)));
            }
            else if ((uint)offset > (uint)pool.MaxSegmentSize)
            {
                task = ValueTask.FromException<int>(new ArgumentOutOfRangeException(nameof(offset)));
            }
            else
            {
                task = pool.ReadAsync(absoluteOffset, buffer, offset, token);
            }

            return task;
        }

        /// <summary>
        /// Creates a stream representing this segment.
        /// </summary>
        /// <remarks>
        /// The returned stream has a length equal to <see cref="DiskSpacePool.MaxSegmentSize"/>.
        /// You can adjust it by calling <see cref="Stream.SetLength"/>.
        /// </remarks>
        /// <returns>A stream representing this segment.</returns>
        public Stream CreateStream() => new SegmentStream(poolRef, absoluteOffset);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (poolRef.TryGetTarget(out var target))
            {
                poolRef.SetTarget(target: null);
                target.ReleaseSegment(absoluteOffset);
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            ValueTask task;
            if (poolRef.TryGetTarget(out var target))
            {
                poolRef.SetTarget(target: null);
                task = target.ReleaseSegmentAsync(absoluteOffset);
            }
            else
            {
                task = ValueTask.CompletedTask;
            }

            return task;
        }

        /// <inheritdoc/>
        public override string ToString() => absoluteOffset.ToString();
    }
    
    /// <summary>
    /// Represents configuration of the pool.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Options
    {
        private readonly int segments;
        private readonly bool normalAllocation;
        
        /// <summary>
        /// Determines whether the asynchronous I/O is preferred.
        /// </summary>
        public bool IsAsynchronous { get; init; }

        /// <summary>
        /// Indicates that the allocation of the data on disk is optimized.
        /// </summary>
        /// <remarks>
        /// The segment typically doesn't contain meaningful payload for a whole size of the segment. To reduce disk
        /// space consumption, this parameter can be set to <see langword="true"/> by the cost of I/O performance.
        /// </remarks>
        public bool OptimizedDiskAllocation
        {
            get => !normalAllocation;
            init => normalAllocation = !value;
        }

        /// <summary>
        /// Gets or sets the expected number of segments to preallocate the disk space.
        /// </summary>
        /// <remarks>
        /// It has no effect if <see cref="OptimizedDiskAllocation"/> is set to <see langword="true"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int ExpectedNumberOfSegments
        {
            get => segments > 0 ? segments : 1;
            init => segments = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal FileOptions FileOptions
        {
            get
            {
                FileOptions result;
                if (OperatingSystem.IsWindows())
                {
                    // Do not use RandomAccess flag on Windows:
                    // https://learn.microsoft.com/en-us/troubleshoot/windows-server/application-management/operating-system-performance-degrades
                    result = FileOptions.DeleteOnClose | FileOptions.WriteThrough;
                }
                else
                {
                    result = FileOptions.DeleteOnClose | FileOptions.RandomAccess | FileOptions.WriteThrough;
                }

                return IsAsynchronous ? result | FileOptions.Asynchronous : result;
            }
        }

        internal FileAttributes FileAttributes
            => normalAllocation ? FileAttributes.NotContentIndexed : FileAttributes.NotContentIndexed | FileAttributes.SparseFile;
    }
}