using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;
    using System.Collections;
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

        private interface ILogEntrySource
        {
            ValueTask<IRaftLogEntry[]> ReadAllAsync(CancellationToken token);
        }

        private readonly struct ListSource : ILogEntrySource
        {
            private readonly IReadOnlyList<IRaftLogEntry> entries;

            internal ListSource(IReadOnlyList<IRaftLogEntry> entries) => this.entries = entries;

            async ValueTask<IRaftLogEntry[]> ILogEntrySource.ReadAllAsync(CancellationToken token)
            {
                var bufferedEntries = new IRaftLogEntry[entries.Count];
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    bufferedEntries[i] = entry.IsReusable
                        ? entry
                        : await BufferedLogEntry.CreateBufferedEntryAsync(entry, token).ConfigureAwait(false);
                }
                return bufferedEntries;
            }
        }

        private readonly struct SingleEntrySource : ILogEntrySource
        {
            private readonly IRaftLogEntry entry;

            internal SingleEntrySource(IRaftLogEntry entry) => this.entry = entry;

            ValueTask<IRaftLogEntry[]> ILogEntrySource.ReadAllAsync(CancellationToken token) => new ValueTask<IRaftLogEntry[]>(new[] { entry });
        }

        private readonly struct EnumeratorSource : ILogEntrySource
        {
            private readonly Func<ValueTask<IRaftLogEntry>> enumerator;

            internal EnumeratorSource(Func<ValueTask<IRaftLogEntry>> enumerator) => this.enumerator = enumerator;

            async ValueTask<IRaftLogEntry[]> ILogEntrySource.ReadAllAsync(CancellationToken token)
            {
                var bufferedEntries = new List<IRaftLogEntry>();
                IRaftLogEntry entry;
                while ((entry = await enumerator().ConfigureAwait(false)) != null)
                    bufferedEntries.Add(entry.IsReusable ? entry : await BufferedLogEntry.CreateBufferedEntryAsync(entry, token).ConfigureAwait(false));
                return bufferedEntries.ToArray();
            }
        }

        private sealed class InitialLogEntry : IRaftLogEntry
        {
            long? IDataTransferObject.Length => 0L;
            Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

            ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask();

            long IRaftLogEntry.Term => 0L;

            bool IDataTransferObject.IsReusable => true;

            DateTimeOffset ILogEntry.Timestamp => default;

            bool ILogEntry.IsSnapshot => false;
        }

        private readonly struct LogEntryList : IReadOnlyList<IRaftLogEntry>
        {
            private readonly long startIndex;
            private readonly long endIndex;
            private readonly IRaftLogEntry[] entries;

            internal LogEntryList(IRaftLogEntry[] entries, long startIndex, long endIndex)
            {
                this.entries = entries;
                this.startIndex = startIndex;
                this.endIndex = endIndex;
            }

            public IRaftLogEntry this[int index] => entries[index + startIndex];

            public int Count => checked((int)(endIndex - startIndex + 1));

            public IEnumerator<IRaftLogEntry> GetEnumerator()
            {
                for (var index = startIndex; index <= endIndex; index++)
                    yield return entries[index];
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static readonly IRaftLogEntry[] InitialLog = { new InitialLogEntry() };

        private long commitIndex, lastApplied;
        private volatile IRaftLogEntry[] log;

        private long term;
        private volatile IRaftClusterMember votedFor;
        private readonly AsyncManualResetEvent commitEvent;
        /// <summary>
        /// Represents reader/writer lock used for synchronized access to this method.
        /// </summary>
        private readonly AsyncReaderWriterLock syncRoot;

        /// <summary>
        /// Initializes a new audit trail with empty log.
        /// </summary>
        public InMemoryAuditTrail()
        {
            lastApplied = -1L;
            log = InitialLog;
            commitEvent = new AsyncManualResetEvent(false);
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

        async ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadEntriesAsync<TReader, TResult>(TReader reader, long startIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            using (await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false))
                return await reader.ReadAsync<IRaftLogEntry, LogEntryList>(new LogEntryList(log, startIndex, GetLastIndex(false)), null, token).ConfigureAwait(false);
        }

        async ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadEntriesAsync<TReader, TResult>(TReader reader, long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return await reader.ReadAsync<BufferedLogEntry, BufferedLogEntry[]>(Array.Empty<BufferedLogEntry>(), null, token);
            using (await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false))
                return endIndex < log.LongLength ? await reader.ReadAsync<IRaftLogEntry, LogEntryList>(new LogEntryList(log, startIndex, endIndex), null, token).ConfigureAwait(false) : throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
        }

        private void Append(IRaftLogEntry[] entries, long startIndex)
        {
            if (startIndex < log.LongLength)
                log = log.RemoveLast(log.LongLength - startIndex);
            var newLog = new IRaftLogEntry[entries.Length + log.LongLength];
            Array.Copy(log, newLog, log.LongLength);
            entries.CopyTo(newLog, log.LongLength);
            log = newLog;
        }

        private async ValueTask<long> AppendAsync<TSource>(TSource entries, long? startIndex, bool skipCommitted, CancellationToken token)
            where TSource : struct, ILogEntrySource
        {
            if (startIndex is null)
                startIndex = log.LongLength;
            else if (startIndex > log.LongLength)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            IRaftLogEntry[] appendingScope;
            if (skipCommitted)
            {
                appendingScope = await entries.ReadAllAsync(token).ConfigureAwait(false);
                var skipNum = Math.Max(0, GetLastIndex(true) - startIndex.Value + 1L);
                appendingScope = appendingScope.RemoveFirst(skipNum);
                startIndex += skipNum;
            }
            else if (startIndex <= GetLastIndex(true))
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            else
                appendingScope = await entries.ReadAllAsync(token).ConfigureAwait(false);
            Append(appendingScope, startIndex.Value);
            return startIndex.Value;
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync(Func<ValueTask<IRaftLogEntry>> supplier, long startIndex, bool skipCommitted, CancellationToken token)
        {
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(new EnumeratorSource(supplier), startIndex, skipCommitted, token);
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
        {
            if (entries.Count == 0)
                return;
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(new ListSource(entries), startIndex, skipCommitted, token).ConfigureAwait(false);
        }

        async ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries, CancellationToken token)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                return await AppendAsync(new ListSource(entries), null, false, token).ConfigureAwait(false);
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync(IRaftLogEntry entry, long startIndex)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(new SingleEntrySource(entry), null, false, default).ConfigureAwait(false);
        }

        private async ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
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
                    commitEvent.Set(true);
                }
            }
            return Math.Max(count, 0L);
        }

        ValueTask<long> IAuditTrail.CommitAsync(long endIndex, CancellationToken token)
            => CommitAsync(endIndex, token);

        ValueTask<long> IAuditTrail.CommitAsync(CancellationToken token)
            => CommitAsync(null, token);

        /// <summary>
        /// Applies the command represented by the log entry to the underlying database engine.
        /// </summary>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask ApplyAsync(IRaftLogEntry entry) => new ValueTask();

        private async ValueTask ApplyAsync(CancellationToken token)
        {
            for (var i = lastApplied.VolatileRead() + 1L; i <= commitIndex.VolatileRead(); token.ThrowIfCancellationRequested(), i++)
            {
                await ApplyAsync(log[i]).ConfigureAwait(false);
                lastApplied.VolatileWrite(i);
            }
        }

        async Task IAuditTrail.EnsureConsistencyAsync(CancellationToken token)
        {
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
                await ApplyAsync(token).ConfigureAwait(false);
        }

        Task IAuditTrail.WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => index >= 0L ? CommitEvent.WaitForCommitAsync(this, commitEvent, index, timeout, token) : Task.FromException(new ArgumentOutOfRangeException(nameof(index)));

        ref readonly IRaftLogEntry IAuditTrail<IRaftLogEntry>.First => ref InitialLog[0];

        /// <summary>
        /// Releases all resources associated with this audit trail.
        /// </summary>
        /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                commitEvent.Dispose();
                syncRoot.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}