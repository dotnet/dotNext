using System;
using System.Buffers;
using System.IO.Compression;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;

    public partial class PersistentState
    {
        /// <summary>
        /// Represents configuration options of the persistent audit trail.
        /// </summary>
        public class Options
        {
            private const int MinBufferSize = 128;
            private int bufferSize = 2048;
            private int concurrencyLevel = 3;

            /// <summary>
            /// Gets size of in-memory buffer for I/O operations.
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
            /// Gets or sets the initial size of the file that holds the partition with log entries.
            /// </summary>
            public long InitialPartitionSize { get; set; }

            /// <summary>
            /// Enables or disables in-memory cache.
            /// </summary>
            /// <value><see langword="true"/> to in-memory cache for faster read/write of log entries; <see langword="false"/> to reduce the memory by the cost of the performance.</value>
            public bool UseCaching { get; set; } = true;

            /// <summary>
            /// Gets memory pool that is used by Write-Ahead Log for its I/O operations.
            /// </summary>
            /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
            /// <returns>The instance of memory pool.</returns>
            [Obsolete("Use GetMemoryAllocator instead")]
            public virtual MemoryPool<T> CreateMemoryPool<T>()
                where T : struct => MemoryPool<T>.Shared;

            /// <summary>
            /// Gets memory allocator for internal purposes.
            /// </summary>
            /// <typeparam name="T">The type of items in the pool.</typeparam>
            /// <returns>The memory allocator.</returns>
            public virtual MemoryAllocator<T> GetMemoryAllocator<T>()
                where T : struct
            {
                // TODO: This code needed for backward compatibility
                Func<MemoryPool<T>> factory = CreateMemoryPool<T>;
                return factory.Method.DeclaringType == typeof(Options) ? ArrayPool<T>.Shared.ToAllocator() : factory().ToAllocator();
            }

            /// <summary>
            /// Gets or sets the number of possible parallel reads.
            /// </summary>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 1.</exception>
            public int MaxConcurrentReads
            {
                get => concurrencyLevel;
                set
                {
                    if (concurrencyLevel < 1)
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
            /// Gets or sets compression level used
            /// to create backup archive.
            /// </summary>
            public CompressionLevel BackupCompression { get; set; } = CompressionLevel.Optimal;
        }
    }
}
