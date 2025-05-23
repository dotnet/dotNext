using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Numerics;
using Threading;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents configuration options.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public class Options
    {
        private readonly int chunkMaxSize = Environment.SystemPageSize;
        private readonly int concurrencyLevel = Environment.ProcessorCount * 2 + 1;
        private readonly string location = string.Empty;
        private readonly TimeSpan flushInterval;

        /// <summary>
        /// Gets or sets the path to the root folder to be used by the log to persist log entries.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [Required]
        public required string Location
        {
            get => location;
            init => location = value is { Length: > 0 }
                ? Path.GetFullPath(value)
                : throw new ArgumentOutOfRangeException(nameof(value));
        }

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
            get => flushInterval;
            init
            {
                Timeout.Validate(value);
                
                flushInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum size of the single chunk file, in bytes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int ChunkMaxSize
        {
            get => chunkMaxSize;
            init
            {
                chunkMaxSize = value > 0
                    ? RoundUpToPageSize(value)
                    : throw new ArgumentOutOfRangeException(nameof(value));
                
                static int RoundUpToPageSize(int value)
                {
                    var result = ((uint)value).RoundUp((uint)Page.MinPageSize);
                    return checked((int)result);
                }
            }
        }

        /// <summary>
        /// Gets or sets an expected number of concurrent users of the log.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int ConcurrencyLevel
        {
            get => concurrencyLevel;
            init => concurrencyLevel = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the memory allocator.
        /// </summary>
        public MemoryAllocator<byte>? Allocator
        {
            get;
            init;
        }
        
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