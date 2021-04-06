using System;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    /// <summary>
    /// Represents buffering options used for batch processing of log entries.
    /// </summary>
    public class RaftLogEntriesBufferingOptions : RaftLogEntryBufferingOptions
    {
        private const int DefaultMemoryLimit = 10 * 1024 * 1024;
        private int memoryLimit = DefaultMemoryLimit;

        /// <summary>
        /// The maximum amount of memory that can be allocated for the buffered log entry.
        /// </summary>
        /// <remarks>
        /// If the limit is reached then the log entries will be stored on the disk.
        /// </remarks>
        public int MemoryLimit
        {
            get => memoryLimit;
            set => memoryLimit = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}