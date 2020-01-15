using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using System.Collections;
    using Threading;

    /// <summary>
    /// Represents lightweight Raft node state that is suitable for distributed consensus only.
    /// </summary>
    /// <remarks>
    /// The actual state doesn't persist on disk and exists only in memory. Moreover, this implementation
    /// cannot append non-empty log entries.
    /// </remarks>
    public sealed class ConsensusOnlyState : Disposable, IPersistentState
    {
        private static readonly IRaftLogEntry First = new EmptyEntry(0L);

        private readonly struct EntryList : IReadOnlyList<EmptyEntry>
        {
            private readonly long count, uncommittedIndex, snapshotTerm;
            private readonly long[] terms;

            internal EntryList(long[] log, long count, long firstUncommitted, long snapshotTerm)
            {
                terms = log;
                this.count = count;
                uncommittedIndex = firstUncommitted;
                this.snapshotTerm = snapshotTerm;
            }

            public EmptyEntry this[int index]
            {
                get
                {
                    if (index < 0 || index >= count)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    return new EmptyEntry(index >= uncommittedIndex ? terms[index] : snapshotTerm);
                }
            }


            int IReadOnlyCollection<EmptyEntry>.Count => (int)count;

            public IEnumerator<EmptyEntry> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        private long term, commitIndex, lastTerm, index;
        private volatile IRaftClusterMember? votedFor;
        private readonly AsyncReaderWriterLock syncRoot = new AsyncReaderWriterLock();
        private readonly AsyncManualResetEvent commitEvent = new AsyncManualResetEvent(false);
        private volatile long[] log = Array.Empty<long>();    //log of uncommitted entries

        long IPersistentState.Term => term.VolatileRead();

        ref readonly IRaftLogEntry IAuditTrail<IRaftLogEntry>.First => ref First;

        private async ValueTask<long> AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long? startIndex, bool skipCommitted, CancellationToken token)
            where TEntryImpl : notnull, IRaftLogEntry
        {
            token.ThrowIfCancellationRequested();
            long skip;
            if (startIndex is null)
                startIndex = index + 1L;
            else if (startIndex > index + 1L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (skipCommitted)
            {
                skip = Math.Max(0, commitIndex.VolatileRead() - startIndex.Value + 1L);
                startIndex += skip;
            }
            else if (startIndex <= commitIndex.VolatileRead())
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            else
                skip = 0L;
            var count = entries.RemainingCount - skip;
            if (count > 0L)
            {
                //skip entries
                var newEntries = new long[count];
                while (skip-- > 0)
                    await entries.MoveNextAsync().ConfigureAwait(false);
                //copy terms
                for (var i = 0; await entries.MoveNextAsync().ConfigureAwait(false) && i < newEntries.LongLength; i++)
                    newEntries[i] = entries.Current.Term;
                //now concat existing array of terms
                log = log.Concat(newEntries, startIndex.Value - commitIndex - 1L);
                index.VolatileWrite(startIndex.Value + count - 1L);
            }
            return startIndex.Value;
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long startIndex, bool skipCommitted, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
                await AppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        }

        async ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, CancellationToken token)
        {
            if (entries.RemainingCount == 0L)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty);
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
                return await AppendAsync(entries, null, false, token).ConfigureAwait(false);
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntryImpl>(TEntryImpl entry, long startIndex)
        {
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(LogEntryProducer<TEntryImpl>.Of(entry), null, false, CancellationToken.None).ConfigureAwait(false);
        }

        private async ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
        {
            long count;
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
            {
                var startIndex = commitIndex.VolatileRead() + 1L;
                count = (endIndex ?? GetLastIndex(false)) - startIndex + 1L;
                if (count > 0)
                {
                    commitIndex.VolatileWrite(startIndex + count - 1);
                    //count indicates how many elements should be removed from log
                    log = log.RemoveFirst(count);
                    commitEvent.Set(true);
                }
            }
            return Math.Max(count, 0L);
        }

        ValueTask<long> IAuditTrail.CommitAsync(long endIndex, CancellationToken token)
            => CommitAsync(endIndex, token);

        ValueTask<long> IAuditTrail.CommitAsync(CancellationToken token)
            => CommitAsync(null, token);

        async ValueTask<long> IAuditTrail<IRaftLogEntry>.DropAsync(long startIndex, CancellationToken token)
        {
            long count;
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
            {
                if (startIndex <= commitIndex.VolatileRead())
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                count = index.VolatileRead() - startIndex + 1L;
                index.VolatileWrite(startIndex - 1L);
            }
            return count;
        }

        /// <summary>
        /// Gets index of the committed or last log entry.
        /// </summary>
        /// <param name="committed"><see langword="true"/> to get the index of highest log entry known to be committed; <see langword="false"/> to get the index of the last log entry.</param>
        /// <returns>The index of the log entry.</returns>
        public long GetLastIndex(bool committed)
            => committed ? commitIndex.VolatileRead() : index.VolatileRead();

        ValueTask<long> IPersistentState.IncrementTermAsync() => new ValueTask<long>(term.IncrementAndGet());

        Task IAuditTrail.InitializeAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        bool IPersistentState.IsVotedFor(IRaftClusterMember? member)
        {
            var lastVote = votedFor;
            return lastVote is null || ReferenceEquals(lastVote, member);
        }

        async ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TReader, TResult>(TReader reader, long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return await reader.ReadAsync<EmptyEntry, EmptyEntry[]>(Array.Empty<EmptyEntry>(), null, token);
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
            {
                endIndex = Math.Max(index.VolatileRead() + 1, endIndex);
                startIndex = Math.Max(0, startIndex);
                var firstOfUncommitted = commitIndex.VolatileRead() - startIndex;
                return await reader.ReadAsync<EmptyEntry, EntryList>(new EntryList(log, endIndex - startIndex + 1, firstOfUncommitted, lastTerm), firstOfUncommitted == 0 ? null : new long?(commitIndex.VolatileRead()), token).ConfigureAwait(false);
            }
        }

        public ValueTask<TResult> ReadAsync<TReader, TResult>(TReader reader, long startIndex, CancellationToken token = default) where TReader : notnull, ILogEntryConsumer<IRaftLogEntry, TResult>
        {
            throw new NotImplementedException();
        }

        ValueTask IPersistentState.UpdateTermAsync(long value)
        {
            term.VolatileWrite(value);
            return new ValueTask();
        }

        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember? member)
        {
            votedFor = member;
            return new ValueTask();
        }

        public Task WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Releases all resources associated with this audit trail.
        /// </summary>
        /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                syncRoot.Dispose();
                commitEvent.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
