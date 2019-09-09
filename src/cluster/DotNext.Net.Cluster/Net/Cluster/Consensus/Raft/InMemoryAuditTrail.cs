using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using System.Collections;
    using Replication;
    using Threading;

    /// <summary>
    /// Represents in-memory audit trail for Raft-based cluster node.
    /// </summary>
    /// <remarks>
    /// In-memory audit trail doesn't support compaction.
    /// It is recommended to use this audit trail for testing purposes only.
    /// </remarks>
    public class InMemoryAuditTrail : Disposable, IPersistentState
    {
        private sealed class BufferedLogEntry : BinaryTransferObject, IRaftLogEntry
        {
            private BufferedLogEntry(ReadOnlyMemory<byte> content, long term, DateTimeOffset timestamp) 
                : base(content)
            {
                Term = term;
                Timestamp = timestamp;
            }

            internal static async Task<BufferedLogEntry> CreateBufferedEntryAsync(IRaftLogEntry entry, CancellationToken token = default)
            {
                ReadOnlyMemory<byte> content;
                using (var ms = new MemoryStream(1024))
                {
                    await entry.CopyToAsync(ms, token).ConfigureAwait(false);
                    ms.Seek(0, SeekOrigin.Begin);
                    content = ms.TryGetBuffer(out var segment)
                        ? segment
                        : new ReadOnlyMemory<byte>(ms.ToArray());
                }

                return new BufferedLogEntry(content, entry.Term, entry.Timestamp.ToUniversalTime());
            }

            bool ILogEntry.IsSnapshot => false;

            public long Term { get; }

            public DateTimeOffset Timestamp { get; }
        }

        private sealed class InitialLogEntry : IRaftLogEntry
        {
            long? IDataTransferObject.Length => 0L;
            Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

            ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask();

            long IRaftLogEntry.Term => 0L;

            bool IDataTransferObject.IsReusable => true;

            bool ILogEntry.IsSnapshot => false;

            DateTimeOffset ILogEntry.Timestamp => default;
        }

        private sealed class LogEntryList : ILogEntryList<IRaftLogEntry>
        {
            private readonly long startIndex;
            private readonly long endIndex;
            private readonly IRaftLogEntry[] entries;
            private AsyncLock.Holder readLock;

            internal LogEntryList(IRaftLogEntry[] entries, long startIndex, long endIndex, AsyncLock.Holder readLock)
            {
                this.entries = entries;
                this.startIndex = startIndex;
                this.endIndex = endIndex;
                this.readLock = readLock;
            }

            public IRaftLogEntry this[int index] => entries[index + startIndex];

            public int Count => checked((int)(endIndex - startIndex + 1));

            public IEnumerator<IRaftLogEntry> GetEnumerator()
            {
                for (var index = startIndex; index <= endIndex; index++)
                    yield return entries[index];
            }

            private void Dispose() => readLock.Dispose();

            void IDisposable.Dispose()
            {
                Dispose();
                GC.SuppressFinalize(this);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            ~LogEntryList() => Dispose();
        }

        internal static readonly IRaftLogEntry[] EmptyLog = { new InitialLogEntry() };

        private long commitIndex, lastApplied;
        private volatile IRaftLogEntry[] log;

        private long term;
        private volatile IRaftClusterMember votedFor;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly ILogEntryList<BufferedLogEntry> emptyLog;
        /// <summary>
        /// Represents reader/writer lock used for synchronized access to this method.
        /// </summary>
        protected readonly AsyncReaderWriterLock syncRoot;

        /// <summary>
        /// Initializes a new audit trail with empty log.
        /// </summary>
        public InMemoryAuditTrail()
        {
            lastApplied = -1L;
            log = EmptyLog;
            commitEvent = new AsyncManualResetEvent(false);
            emptyLog = new LogEntryList<BufferedLogEntry>();
            syncRoot = new AsyncReaderWriterLock();
        }

        long IPersistentState.Term => term.VolatileRead();

        bool IPersistentState.IsVotedFor(IRaftClusterMember member)
        {
            var lastVote = votedFor;
            return lastVote is null || ReferenceEquals(lastVote, member);
        }

        ValueTask IPersistentState.UpdateTermAsync(long value)
        {
            term.VolatileWrite(value);
            return new ValueTask();
        }

        ValueTask<long> IPersistentState.IncrementTermAsync() => new ValueTask<long>(term.IncrementAndGet());

        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember member)
        {
            votedFor = member;
            return new ValueTask();
        }

        /// <summary>
        /// Gets index of the committed or last log entry.
        /// </summary>
        /// <param name="committed"><see langword="true"/> to get the index of highest log entry known to be committed; <see langword="false"/> to get the index of the last log entry.</param>
        /// <returns>The index of the log entry.</returns>
        public long GetLastIndex(bool committed)
            => committed ? commitIndex.VolatileRead() : Math.Max(0, log.LongLength - 1L);

        async Task<ILogEntryList<IRaftLogEntry>> IAuditTrail<IRaftLogEntry>.GetEntriesAsync(long startIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            var readLock = await this.AcquireReadLockAsync(token).ConfigureAwait(false);
            return new LogEntryList(log, startIndex, GetLastIndex(false), readLock);
        }

        async Task<ILogEntryList<IRaftLogEntry>> IAuditTrail<IRaftLogEntry>.GetEntriesAsync(long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return emptyLog;
            var readLock = await this.AcquireReadLockAsync(token).ConfigureAwait(false);
            if (endIndex >= log.Length)
            {
                readLock.Dispose();
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            }
            return new LogEntryList(log, startIndex, endIndex, readLock);
        }

        private void Append(IRaftLogEntry[] entries, long startIndex)
        {
            if(startIndex < log.LongLength)
                log = log.RemoveLast(log.LongLength - startIndex);
            var newLog = new IRaftLogEntry[entries.Length + log.LongLength];
            Array.Copy(log, newLog, log.LongLength);
            entries.CopyTo(newLog, log.LongLength);
            log = newLog;
        }

        private async Task<long> AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long? startIndex)
        {
            if(startIndex is null)
                startIndex = log.LongLength;
            else if(startIndex <= GetLastIndex(true))
                throw new InvalidOperationException();
            else if(startIndex > log.LongLength)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            var bufferedEntries = new IRaftLogEntry[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bufferedEntries[i] = entry.IsReusable
                    ? entry
                    : await BufferedLogEntry.CreateBufferedEntryAsync(entry).ConfigureAwait(false);
            }
            Append(bufferedEntries, startIndex.Value);
            return startIndex.Value;
        }

        async Task IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(entries, startIndex).ConfigureAwait(false);
        }
        
        async Task<long> IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                return await AppendAsync(entries, null).ConfigureAwait(false);
        }

        private async Task<long> CommitAsync(long? endIndex, CancellationToken token)
        {
            long count;
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
            {
                var startIndex = GetLastIndex(true) + 1L;
                count = (endIndex ?? GetLastIndex(false)) - startIndex + 1L;
                if (count > 0)
                {
                    commitIndex.VolatileWrite(startIndex + count - 1);
                    await ApplyAsync(token).ConfigureAwait(false);
                }
            }
            return count;
        }

        Task<long> IAuditTrail.CommitAsync(long endIndex, CancellationToken token)
            => CommitAsync(endIndex, token);
        
        Task<long> IAuditTrail.CommitAsync(CancellationToken token)
            => CommitAsync(null, token);

        /// <summary>
        /// Applies the command represented by the log entry to the underlying database engine.
        /// </summary>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask ApplyAsync(IRaftLogEntry entry) => new ValueTask(Task.CompletedTask);

        private async Task ApplyAsync(CancellationToken token)
        {
            for(var i = lastApplied.VolatileRead() + 1L; i <= commitIndex.VolatileRead(); token.ThrowIfCancellationRequested(), i++)
            {
                await ApplyAsync(log[i]).ConfigureAwait(false);
                lastApplied.VolatileWrite(i);
            }
        }

        async Task IAuditTrail.EnsureConsistencyAsync(CancellationToken token)
        {
            using(await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
                await ApplyAsync(token).ConfigureAwait(false);
        }

        Task IAuditTrail.WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => index >= 0L ? CommitEvent.WaitForCommitAsync(this, commitEvent, index, timeout, token) : Task.FromException(new ArgumentOutOfRangeException(nameof(index)));

        ref readonly IRaftLogEntry IAuditTrail<IRaftLogEntry>.First => ref EmptyLog[0];

        /// <summary>
        /// Releases all resources associated with this audit trail.
        /// </summary>
        /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                emptyLog.Dispose();
                commitEvent.Dispose();
                syncRoot.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}