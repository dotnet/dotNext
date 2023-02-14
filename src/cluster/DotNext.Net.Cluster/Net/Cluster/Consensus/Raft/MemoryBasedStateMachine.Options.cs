using System.Diagnostics.Tracing;

namespace DotNext.Net.Cluster.Consensus.Raft;

public partial class MemoryBasedStateMachine
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
        /// <see cref="ApplyAsync(LogEntry)"/> has approx the same execution
        /// time as <see cref="SnapshotBuilder.ApplyAsync(LogEntry)"/>.
        /// </remarks>
        Foreground = 2,

        /// <summary>
        /// Log compaction is executing for each committed log entry and flushes
        /// the snapshot only when the partition overflows.
        /// </summary>
        /// <remarks>
        /// This log compaction algorithm is a combination of <see cref="Foreground"/>
        /// and <see cref="Sequential"/>.
        /// This compaction mode doesn't use <see cref="SnapshotBuilder.AdjustIndex(long, long, ref long)"/>
        /// method.
        /// </remarks>
        Incremental = 3,
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
    /// Represents configuration options of memory-based state machine.
    /// </summary>
    public new class Options : PersistentState.Options
    {
        private int? snapshotBufferSize;

        /// <summary>
        /// Gets or sets size of in-memory buffer for I/O operations associated with
        /// the construction of log snapshot.
        /// </summary>
        /// <remarks>
        /// By default, the value of this buffer is equal to <see cref="PersistentState.Options.BufferSize"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is too small.</exception>
        public int SnapshotBufferSize
        {
            get => snapshotBufferSize ?? BufferSize;
            set
            {
                if (value < MinBufferSize)
                    throw new ArgumentOutOfRangeException(nameof(value));

                snapshotBufferSize = value;
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
        /// Gets or sets eviction policy for the cache of buffered log entries.
        /// </summary>
        /// <remarks>
        /// This property has no effect is <see cref="PersistentState.Options.UseCaching"/> is <see langword="false"/>.
        /// </remarks>
        /// <seealso cref="PersistentState.AppendAsync{TEntry}(TEntry, bool, CancellationToken)"/>
        public LogEntryCacheEvictionPolicy CacheEvictionPolicy
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the counter used to measure the number of squashed log entries.
        /// </summary>
        [Obsolete("Use System.Diagnostics.Metrics infrastructure instead.", UrlFormat = "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics")]
        public IncrementingEventCounter? CompactionCounter
        {
            get;
            set;
        }
    }
}