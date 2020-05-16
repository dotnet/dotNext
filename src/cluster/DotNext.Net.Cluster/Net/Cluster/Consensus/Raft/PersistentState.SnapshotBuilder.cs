using System;
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
                return entry.IsEmpty ? new ValueTask() : ApplyAsync(entry);
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
