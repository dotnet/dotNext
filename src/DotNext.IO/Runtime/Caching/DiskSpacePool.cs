using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

/// <summary>
/// Represents a pool of segments of the limited size on the disk.
/// </summary>
/// <remarks>
/// This class can be used to organize on-disk cache in combination with <c>RandomAccessCache</c>.
/// All members of this class are thread-safe.
/// </remarks>
[DebuggerDisplay($"PooledSegments = {{{nameof(ReturnedSegments)}}}")]
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
        if (options.DontCleanDiskSpace)
        {
            preallocationSize = options.ExpectedNumberOfSegments * (long)maxSegmentSize;
        }
        else
        {
            zeroes = GC.AllocateArray<byte>(maxSegmentSize, pinned: true);
            preallocationSize = 0L;
        }

        handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, options.FileOptions, preallocationSize);
        File.SetAttributes(handle, options.FileAttributes);
        MaxSegmentSize = maxSegmentSize;
        cursor = -maxSegmentSize;

        if (OperatingSystem.IsLinux())
        {
            unsafe
            {
                var posix_fadvise =
                    (delegate*unmanaged<nint, long, long, int, int>)NativeLibrary.GetExport(
                        NativeLibrary.GetMainProgramHandle(),
                        "posix_fadvise");

                if (posix_fadvise is not null)
                {
                    const int POSIX_FADV_NOREUSE = 5;
                    var errorCode = posix_fadvise(handle.DangerousGetHandle(), 0L, 0L, POSIX_FADV_NOREUSE);
                    Debug.Assert(errorCode is 0);
                }
            }
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
            for (var node = head; node is not null; node = node.Next)
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
            head = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Provides the random access to the data within the segment.
    /// </summary>
    public sealed class Segment : Disposable, IAsyncDisposable
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

            if (offset < 0 || offset + buffer.Length > pool.MaxSegmentSize)
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
                task = new(DisposedTask);
            }
            else if (offset < 0 || offset + buffer.Length > pool.MaxSegmentSize)
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
                task = new(GetDisposedTask<int>());
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

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (poolRef.TryGetTarget(out var target))
                {
                    poolRef.SetTarget(target: null);
                    target.ReleaseSegment(absoluteOffset);
                }
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override ValueTask DisposeAsyncCore()
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

        /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
        public new ValueTask DisposeAsync() => base.DisposeAsync();

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
        
        /// <summary>
        /// Determines whether the asynchronous I/O is preferred.
        /// </summary>
        public bool IsAsynchronous { get; init; }
        
        /// <summary>
        /// Determines whether the pool should not clean up the disk space occupied by the released segment.
        /// </summary>
        public bool DontCleanDiskSpace { get; init; }

        /// <summary>
        /// Gets or sets the expected number of segments to preallocate the disk space.
        /// </summary>
        /// <remarks>
        /// It has no effect if <see cref="DontCleanDiskSpace"/> is set to <see langword="false"/>.
        /// </remarks>
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
            => DontCleanDiskSpace ? FileAttributes.NotContentIndexed : FileAttributes.NotContentIndexed | FileAttributes.SparseFile;
    }
}