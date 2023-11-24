using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO.Log;

public partial class PersistentState
{
    internal interface IBufferManagerSettings
    {
        MemoryAllocator<T> GetMemoryAllocator<T>();

        bool UseCaching { get; }
    }

    /// <summary>
    /// Describes how the log interacts with underlying storage device.
    /// </summary>
    public enum WriteMode
    {
        /// <summary>
        /// Delegates intermediate buffer flush to operating system.
        /// </summary>
        NoFlush = 0,

        /// <summary>
        /// Flushes data to disk only if the internal buffer oveflows.
        /// </summary>
        AutoFlush,

        /// <summary>
        /// Bypass intermediate buffers for all disk writes.
        /// </summary>
        WriteThrough,
    }

    /// <summary>
    /// Represents configuration options of the persistent audit trail.
    /// </summary>
    public class Options : IBufferManagerSettings
    {
        private protected const int MinBufferSize = 128;
        private int bufferSize = 4096;
        private int concurrencyLevel = Math.Max(3, Environment.ProcessorCount);
        private long partitionSize;
        private bool parallelIO;

        /// <summary>
        /// Gets or sets a value indicating how the log interacts with underlying storage device.
        /// </summary>
        public WriteMode WriteMode { get; set; } = WriteMode.AutoFlush;

        /// <summary>
        /// Gets or sets size of in-memory buffer for I/O operations.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is too small.</exception>
        public int BufferSize
        {
            get => bufferSize;
            set
            {
                if (value < MinBufferSize)
                    throw new ArgumentOutOfRangeException(nameof(value));
                bufferSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the initial size of the file that holds the partition with log entries, in bytes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than zero.</exception>
        public long InitialPartitionSize
        {
            get => partitionSize;
            set => partitionSize = value >= 0L ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Enables or disables in-memory cache.
        /// </summary>
        /// <value><see langword="true"/> to in-memory cache for faster read/write of log entries; <see langword="false"/> to reduce the memory by the cost of the performance.</value>
        public bool UseCaching { get; set; } = true;

        /// <summary>
        /// Enables or disables integrity check of the internal WAL state.
        /// </summary>
        /// <remarks>
        /// The default value is <see langword="false"/> for backward compatibility.
        /// </remarks>
        public bool IntegrityCheck { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that the underlying storage device
        /// can perform read/write operations simultaneously.
        /// </summary>
        /// <remarks>
        /// This parameter makes no sense if <see cref="UseCaching"/> is <see langword="true"/>.
        /// If caching is disabled, set this property to <see langword="true"/> if the underlying
        /// storage is attached using parallel interface such as NVMe (via PCIe bus).
        /// The default value is <see langword="false"/>.
        /// </remarks>
        public bool ParallelIO
        {
            get => UseCaching || parallelIO;
            set => parallelIO = value;
        }

        /// <summary>
        /// Gets memory allocator for internal purposes.
        /// </summary>
        /// <typeparam name="T">The type of items in the pool.</typeparam>
        /// <returns>The memory allocator.</returns>
        public virtual MemoryAllocator<T> GetMemoryAllocator<T>() => ArrayPool<T>.Shared.ToAllocator();

        /// <summary>
        /// Gets or sets the number of possible parallel reads.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 2.</exception>
        public int MaxConcurrentReads
        {
            get => concurrencyLevel;

            // see LockManager ctor for more explanation
            set => concurrencyLevel = value is >= 2 and <= int.MaxValue - 2 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets compression level used
        /// to create backup archive.
        /// </summary>
        public CompressionLevel BackupCompression { get; set; } = CompressionLevel.Optimal;

        /// <summary>
        /// If set then every read operations will be performed
        /// on buffered copy of the log entries.
        /// </summary>
        public RaftLogEntriesBufferingOptions? CopyOnReadOptions
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a list of tags to be associated with each measurement.
        /// </summary>
        [CLSCompliant(false)]
        public TagList MeasurementTags
        {
            get;
            set;
        }

        internal BufferingLogEntryConsumer? CreateBufferingConsumer()
            => CopyOnReadOptions is null ? null : new BufferingLogEntryConsumer(CopyOnReadOptions);
    }
}