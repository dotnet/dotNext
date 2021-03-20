using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Generic;
    using IO.Log;
    using Threading.Tasks;
    using static Runtime.Intrinsics;

    /// <summary>
    /// Represents log entry producer that allows to bufferize log entries
    /// from another producer.
    /// </summary>
    /// <remarks>
    /// This class typically used by custom implementations of transport layer for Raft.
    /// </remarks>
    public sealed class BufferedRaftLogEntryProducer : Disposable, ILogEntryProducer<BufferedRaftLogEntry>, IEnumerator<BufferedRaftLogEntry>
    {
        private readonly BufferedRaftLogEntry[] entries;
        private nint offset;

        private BufferedRaftLogEntryProducer(BufferedRaftLogEntry[] entries)
        {
            this.entries = entries;
            offset = -1;
        }

        /// <summary>
        /// Constructs bufferized copy of all log entries presented in the sequence.
        /// </summary>
        /// <param name="producer">The sequence of log entries to be copied.</param>
        /// <param name="options">Buffering options.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TEntry">The type of the entry in the source sequence.</typeparam>
        /// <returns>The copy of the log entries.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task<BufferedRaftLogEntryProducer> CopyAsync<TEntry>(ILogEntryProducer<TEntry> producer, RaftLogEntryBufferingOptions options, CancellationToken token = default)
            where TEntry : notnull, IRaftLogEntry
        {
            var entries = new BufferedRaftLogEntry[producer.RemainingCount];
            for (nint index = 0; await producer.MoveNextAsync().ConfigureAwait(false); index++)
            {
                entries[index] = await BufferedRaftLogEntry.CopyAsync(producer.Current, options, token).ConfigureAwait(false);
            }

            return new BufferedRaftLogEntryProducer(entries);
        }

        /// <summary>
        /// Gets log entry at the current position in the sequence.
        /// </summary>
        public BufferedRaftLogEntry Current => entries[offset];

        /// <inheritdoc />
        object IEnumerator.Current => Current;

        /// <summary>
        /// Gets remaining count of log entries in this sequence.
        /// </summary>
        public long RemainingCount
        {
            get
            {
                ThrowIfDisposed();
                var offset = this.offset;
                if (offset < 0)
                    offset = 0;

                return GetLength(entries) - offset;
            }
        }

        /// <summary>
        /// Moves to the next bufferized log entry in the sequence.
        /// </summary>
        /// <returns><see langword="true"/> if cursor is adjusted to the next log entry; <see langword="false"/> if the end of the sequence reached.</returns>
        public bool MoveNext()
        {
            if (offset < 0)
                offset = 0;
            else
                offset += 1;

            return offset < GetLength(entries);
        }

        /// <inheritdoc />
        ValueTask<bool> IAsyncEnumerator<BufferedRaftLogEntry>.MoveNextAsync()
            => new ValueTask<bool>(MoveNext() ? CompletedTask<bool, BooleanConst.True>.Task : CompletedTask<bool, BooleanConst.False>.Task);

        /// <summary>
        /// Resets this producer so the caller can reuse this
        /// object to iterate over log entries again.
        /// </summary>
        public void Reset() => offset = -1;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (ref var entry in entries.AsSpan())
                {
                    entry.Dispose();
                    entry = default;
                }
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync(false);
    }
}