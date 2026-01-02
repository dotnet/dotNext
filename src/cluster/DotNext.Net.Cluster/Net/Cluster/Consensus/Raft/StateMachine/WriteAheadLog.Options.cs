using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Numerics;
using Threading;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents the type of the memory used by the WAL internals
    /// to keep the written log entries.
    /// </summary>
    public enum MemoryManagementStrategy
    {
        /// <summary>
        /// Log entries are written directly to the memory-mapped file representing
        /// log chunk.
        /// </summary>
        /// <remarks>
        /// This is balanced strategy because the OS can swap out pages automatically
        /// in the case of the memory pressure, as well as flush written log entries
        /// in the background.
        /// </remarks>
        SharedMemory = 0,
        
        /// <summary>
        /// Log entries are written to the temporary private buffer.
        /// </summary>
        /// <remarks>
        /// This strategy is more RAM hungry, but it provides the best write performance.
        /// </remarks>
        PrivateMemory,
    }

    /// <summary>
    /// Represents the hash algorithm that control the integrity of the log entries.
    /// </summary>
    public enum IntegrityHashAlgorithm : byte
    {
        /// <summary>
        /// No integrity check is performed.
        /// </summary>
        None = 0,

        /// <summary>
        /// <see cref="System.IO.Hashing.Crc32"/> is applied for integrity check.
        /// </summary>
        Crc32,

        /// <summary>
        /// <see cref="System.IO.Hashing.Crc64"/> is applied for integrity check.
        /// </summary>
        Crc64,

        /// <summary>
        /// <see cref="System.IO.Hashing.XxHash32"/> is applied for integrity check.
        /// </summary>
        XxHash32,

        /// <summary>
        /// <see cref="System.IO.Hashing.XxHash64"/> is applied for integrity check.
        /// </summary>
        XxHash64,

        /// <summary>
        /// <see cref="System.IO.Hashing.XxHash3"/> is applied for integrity check.
        /// </summary>
        XxHash3,
    }

    /// <summary>
    /// Represents configuration options.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public class Options
    {
        /// <summary>
        /// Gets or sets the path to the root folder to be used by the log to persist log entries.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [Required]
        public required string Location
        {
            get;
            init => field = value is { Length: > 0 }
                ? Path.GetFullPath(value)
                : throw new ArgumentOutOfRangeException(nameof(value));
        } = string.Empty;

        /// <summary>
        /// Gets or sets the interval of the checkpoint.
        /// </summary>
        /// <value>
        /// Use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable automatic
        /// checkpoints. The checkpoint must be triggered manually by calling <see cref="FlushAsync(CancellationToken)"/>
        /// method. Use <see cref="TimeSpan.Zero"/> to enable automatic checkpoint on every commit.
        /// Otherwise, the checkpoint is produced every specified time interval.
        /// </value>
        public TimeSpan FlushInterval
        {
            get;
            init
            {
                Timeout.Validate(value);

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum size of the single chunk file, in bytes.
        /// </summary>
        /// <remarks>
        /// The property cannot be changed for existing WAL.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int ChunkSize
        {
            get;
            init
            {
                field = value > 0
                    ? RoundUpToPageSize(value)
                    : throw new ArgumentOutOfRangeException(nameof(value));

                static int RoundUpToPageSize(int value)
                {
                    var result = ((uint)value).RoundUp((uint)Page.MinSize);
                    return checked((int)result);
                }
            }
        } = Environment.SystemPageSize;

        /// <summary>
        /// Gets or sets an expected number of concurrent users of the log.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int ConcurrencyLevel
        {
            get;
            init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        } = Environment.ProcessorCount * 2 + 1;

        /// <summary>
        /// Gets or sets the memory allocator.
        /// </summary>
        public MemoryAllocator<byte>? Allocator
        {
            get;
            init;
        }
        
        /// <summary>
        /// Gets or sets the memory management strategy.
        /// </summary>
        public MemoryManagementStrategy MemoryManagement { get; init; }

        /// <summary>
        /// Gets or sets the hash algorithm for the log integrity control.
        /// </summary>
        /// <remarks>
        /// Once WAL created, the hash algorithm should not be changed. However, it's possible to migrate
        /// log entries to a different log with modified or disabled hash algorithm with <see cref="WriteAheadLog.ImportAsync"/>
        /// method.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The hash algorithm is not supported.</exception>
        public IntegrityHashAlgorithm HashAlgorithm
        {
            get;
            init => field = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        
        internal NonCryptographicHashAlgorithm? CreateHashAlgorithm() => HashAlgorithm switch
        {
            IntegrityHashAlgorithm.Crc32 => new Crc32(),
            IntegrityHashAlgorithm.Crc64 => new Crc64(),
            IntegrityHashAlgorithm.XxHash32 => new XxHash32(),
            IntegrityHashAlgorithm.XxHash64 => new XxHash64(),
            IntegrityHashAlgorithm.XxHash3 => new XxHash3(),
            _ => null,
        };
        
        /// <summary>
        /// Gets or sets a list of tags to be associated with each measurement.
        /// </summary>
        [CLSCompliant(false)]
        public TagList MeasurementTags
        {
            get;
            init;
        }
    }
}