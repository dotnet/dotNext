using System;
using System.Buffers;
using System.IO.Compression;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO.Log;

    public partial class PersistentState
    {
        /// <summary>
        /// Represents log compaction mode.
        /// </summary>
        public enum CompactionMode : byte
        {
            /// <summary>
            /// Log compaction forced automatically during the commit process
            /// which tries to squash as many committed entries as possible.
            /// </summary>
            /// <remarks>
            /// It demonstrates the worst performance of the commit procedure in
            /// combination with the most aggressive compaction that allows to minimize
            /// usage of disk space.
            /// </remarks>
            Sequential = 0,

            /// <summary>
            /// Log compaction should be triggered manually with <see cref="ForceCompactionAsync(long, CancellationToken)"/>
            /// in the background.
            /// </summary>
            /// <remarks>
            /// Commit and log compaction don't interfere with each other so the commit
            /// procedure demonstrates the best performance. However, this mode requires
            /// more disk space because log compaction is executing in the background and
            /// may be slower than commits.
            /// </remarks>
            Background = 1,

            /// <summary>
            /// Log compaction is executing automatically in parallel with the commit process.
            /// </summary>
            /// <remarks>
            /// Demonstrates the best ratio between the performance of the commit process
            /// and the log compaction. This mode provides the best efficiency if
            /// <see cref="ApplyAsync(CancellationToken)"/> has approx the same execution
            /// time as <see cref="SnapshotBuilder.ApplyAsync(LogEntry)"/>.
            /// </remarks>
            Foreground = 2,
        }

        /// <summary>
        /// Represents eviction policy of the entries located in the cache.
        /// </summary>
        public enum LogEntryCacheEvictionPolicy : byte
        {
            /// <summary>
            /// Cached log entry is evicted only if committed.
            /// </summary>
            OnCommit = 0,

            /// <summary>
            /// Cached log entry remains alive until it will be snapshotted.
            /// </summary>
            /// <remarks>
            /// The commit doesn't cause cache eviction.
            /// </remarks>
            OnSnapshot,
        }

        /// <summary>
        /// Represents configuration options of the persistent audit trail.
        /// </summary>
        public class Options
        {
            private const int MinBufferSize = 128;
            private int bufferSize = 2048;
            private int? snapshotBufferSize;
            private int concurrencyLevel = Math.Max(3, Environment.ProcessorCount);
            private long partitionSize;

            /// <summary>
            /// Gets or sets a value indicating usage of intermediate buffers during I/O.
            /// </summary>
            /// <value>
            /// <see langword="true"/> to bypass intermediate buffers for disk writes.
            /// </value>
            public bool WriteThrough
            {
                get;
                set;
            }

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
            /// Gets or sets size of in-memory buffer for I/O operations associated with
            /// the construction of log snapshot.
            /// </summary>
            /// <remarks>
            /// By default, the value of this buffer is equal to <see cref="BufferSize"/>.
            /// </remarks>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is too small.</exception>
            public int SnapshotBufferSize
            {
                get => snapshotBufferSize ?? bufferSize;
                set
                {
                    if (value < MinBufferSize)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    snapshotBufferSize = value;
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
                set
                {
                    if (concurrencyLevel < 2)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    concurrencyLevel = value;
                }
            }

            /// <summary>
            /// Gets value indicating that dataset
            /// should be reconstructed when <see cref="InitializeAsync(System.Threading.CancellationToken)"/>
            /// method is called.
            /// </summary>
            /// <remarks>
            /// The default value is <see langword="true"/>.
            /// </remarks>
            public bool ReplayOnInitialize { get; set; } = true;

            /// <summary>
            /// Gets or sets log compaction mode.
            /// </summary>
            public CompactionMode CompactionMode { get; set; }

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
            /// Gets or sets eviction policy for the cache of buffered log entries.
            /// </summary>
            /// <remarks>
            /// This property has no effect is <see cref="UseCaching"/> is <see langword="false"/>.
            /// </remarks>
            /// <seealso cref="AppendAsync{TEntry}(TEntry, bool, CancellationToken)"/>
            public LogEntryCacheEvictionPolicy CacheEvictionPolicy
            {
                get;
                set;
            }

            internal ILogEntryConsumer<IRaftLogEntry, (BufferedRaftLogEntryList, long?)>? CreateBufferingConsumer()
                => CopyOnReadOptions is null ? null : new BufferingLogEntryConsumer(CopyOnReadOptions);
        }
    }
}
