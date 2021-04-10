using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using IO.Log;

    public partial class PersistentState
    {
        /// <summary>
        /// Represents snapshot builder.
        /// </summary>
        protected abstract class SnapshotBuilder : Disposable, IRaftLogEntry
        {
            private readonly DateTimeOffset timestamp;
            private long term;

            /// <summary>
            /// Initializes a new snapshot builder.
            /// </summary>
            protected SnapshotBuilder() => timestamp = DateTimeOffset.UtcNow;

            /// <summary>
            /// Interprets the command specified by the log entry.
            /// </summary>
            /// <param name="entry">The committed log entry.</param>
            /// <returns>The task representing asynchronous execution of this method.</returns>
            protected abstract ValueTask ApplyAsync(LogEntry entry);

            internal ValueTask ApplyCoreAsync(LogEntry entry)
            {
                term = Math.Max(entry.Term, term);

                // drop empty log entries during snapshot construction
                return entry.IsEmpty ? new () : ApplyAsync(entry);
            }

            /// <summary>
            /// Allows to adjust the index of the current log entry to be snapshotted.
            /// </summary>
            /// <remarks>
            /// If <paramref name="currentIndex"/> is modified in a way when it out of bounds
            /// then snapshot process will be terminated immediately. Moreover,
            /// compaction algorithm is optimized for monothonic growth of this index.
            /// Stepping back or random access may slow down the process.
            /// </remarks>
            /// <param name="startIndex">The lower bound of the index, inclusive.</param>
            /// <param name="endIndex">The upper bound of the index, inclusive.</param>
            /// <param name="currentIndex">The currently running index.</param>
            [EditorBrowsable(EditorBrowsableState.Advanced)]
            protected internal virtual void AdjustIndex(long startIndex, long endIndex, ref long currentIndex)
            {
            }

            /// <inheritdoc/>
            long? IDataTransferObject.Length => null;

            /// <inheritdoc/>
            long IRaftLogEntry.Term => term;

            /// <inheritdoc/>
            DateTimeOffset ILogEntry.Timestamp => timestamp;

            /// <inheritdoc/>
            bool IDataTransferObject.IsReusable => false;

            /// <inheritdoc/>
            bool ILogEntry.IsSnapshot => true;

            /// <summary>
            /// Serializes the snapshotted entry.
            /// </summary>
            /// <param name="writer">The binary writer.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <typeparam name="TWriter">The type of binary writer.</typeparam>
            /// <returns>The task representing state of asynchronous execution.</returns>
            public abstract ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                where TWriter : IAsyncBinaryWriter;
        }

        /// <summary>
        /// Creates a new snapshot builder.
        /// </summary>
        /// <returns>The snapshot builder; or <see langword="null"/> if snapshotting is not supported.</returns>
        protected virtual SnapshotBuilder? CreateSnapshotBuilder() => null;
    }
}
