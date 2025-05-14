using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;

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

        /// <summary>
        /// Gets or sets the path to the root folder to be used by the log to persist log entries.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public required string Location
        {
            get => location;
            init => location = value is { Length: > 0 } && Path.IsPathFullyQualified(value)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the maximum size of the single chunk file.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int ChunkMaxSize
        {
            get => chunkMaxSize;
            init => chunkMaxSize = value > 0
                ? (int)BitOperations.RoundUpToPowerOf2((uint)value)
                : throw new ArgumentOutOfRangeException(nameof(value));
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