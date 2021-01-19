﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Threading;
    using static Replication.CommitEvent;

    /// <summary>
    /// Represents lightweight Raft node state that is suitable for distributed consensus only.
    /// </summary>
    /// <remarks>
    /// The actual state doesn't persist on disk and exists only in memory. Moreover, this implementation
    /// cannot append non-empty log entries.
    /// </remarks>
    public sealed class ConsensusOnlyState : Disposable, IPersistentState
    {
        private static readonly Func<ConsensusOnlyState, long, bool> IsCommittedPredicate = IsCommitted;

        [StructLayout(LayoutKind.Auto)]
        private readonly struct EntryList : IReadOnlyList<EmptyLogEntry>
        {
            private readonly long count, offset, snapshotTerm;
            private readonly long[] terms;

            internal EntryList(long[] log, long count, long offset, long snapshotTerm)
            {
                Debug.Assert(offset + count <= log.LongLength);
                terms = log;
                this.count = count;
                this.offset = offset;
                this.snapshotTerm = snapshotTerm;
            }

            public EmptyLogEntry this[long index]
            {
                get
                {
                    if (index < 0L || index >= count)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    EmptyLogEntry result;
                    if (offset >= 0L)
                    {
                        result = new EmptyLogEntry(terms[index + offset], false);
                    }
                    else if (index == 0)
                    {
                        result = new EmptyLogEntry(snapshotTerm, true);
                    }
                    else
                    {
                        result = new EmptyLogEntry(terms[index - 1L], false);
                    }

                    return result;
                }
            }

            EmptyLogEntry IReadOnlyList<EmptyLogEntry>.this[int index]
                => this[index];

            int IReadOnlyCollection<EmptyLogEntry>.Count => (int)(offset >= 0 ? count : Math.Max(1L, count + offset + 1L));

            public IEnumerator<EmptyLogEntry> GetEnumerator()
            {
                if (offset < 0)
                    yield return new EmptyLogEntry(snapshotTerm, true);
                for (var i = 0L; i < count + offset; i++)
                    yield return new EmptyLogEntry(terms[i], false);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private readonly AsyncReaderWriterLock syncRoot = new AsyncReaderWriterLock();
        private readonly AsyncManualResetEvent commitEvent = new AsyncManualResetEvent(false);
        private long term, commitIndex, lastTerm, index;
        private volatile IRaftClusterMember? votedFor;
        private volatile long[] log = Array.Empty<long>();    // log of uncommitted entries

        /// <inheritdoc/>
        long IPersistentState.Term => term.VolatileRead();

        /// <inheritdoc/>
        bool IAuditTrail.IsLogEntryLengthAlwaysPresented => true;

        private static bool IsCommitted(ConsensusOnlyState state, long index) => index <= state.commitIndex.VolatileRead();

        private void Append(long[] terms, long startIndex)
        {
            log = log.Concat(terms, startIndex - commitIndex.VolatileRead() - 1L);
            index.VolatileWrite(startIndex + terms.LongLength - 1L);
        }

        private async ValueTask<long> AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long? startIndex, bool skipCommitted, CancellationToken token)
            where TEntryImpl : notnull, IRaftLogEntry
        {
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
            {
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            }
            else
            {
                skip = 0L;
            }

            var count = entries.RemainingCount - skip;
            if (count > 0L)
            {
                // skip entries
                var newEntries = new long[count];
                for (; skip-- > 0; token.ThrowIfCancellationRequested())
                    await entries.MoveNextAsync().ConfigureAwait(false);

                // copy terms
                for (var i = 0; await entries.MoveNextAsync().ConfigureAwait(false) && i < newEntries.LongLength; i++, token.ThrowIfCancellationRequested())
                {
                    if (entries.Current.IsSnapshot)
                        throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);
                    newEntries[i] = entries.Current.Term;
                }

                // now concat existing array of terms
                Append(newEntries, startIndex.Value);
            }

            return startIndex.Value;
        }

        /// <inheritdoc/>
        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long startIndex, bool skipCommitted, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
                await AppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, CancellationToken token)
        {
            if (entries.RemainingCount == 0L)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty);
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
                return await AppendAsync(entries, null, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(TEntry entry, long startIndex)
        {
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (startIndex <= commitIndex.VolatileRead())
                {
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                }
                else if (entry.IsSnapshot)
                {
                    lastTerm.VolatileWrite(entry.Term);
                    commitIndex.VolatileWrite(startIndex);
                    index.VolatileWrite(startIndex);
                    log = Array.Empty<long>();
                    commitEvent.Set(true);
                }
                else if (startIndex > index.VolatileRead() + 1L)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex));
                }
                else
                {
                    long[] singleEntryTerm = { entry.Term };
                    Append(singleEntryTerm, startIndex);
                }
            }
        }

        /// <inheritdoc/>
        async ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(TEntry entry, CancellationToken token)
        {
            using (await syncRoot.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                return await AppendAsync(LogEntryProducer<TEntry>.Of(entry), null, false, CancellationToken.None).ConfigureAwait(false);
        }

        private async ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
        {
            long count;
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
            {
                var startIndex = commitIndex.VolatileRead() + 1L;
                count = (endIndex ?? index.VolatileRead()) - startIndex + 1L;
                if (count > 0)
                {
                    commitIndex.VolatileWrite(startIndex + count - 1);
                    lastTerm.VolatileWrite(log[count - 1]);

                    // count indicates how many elements should be removed from log
                    log = log.RemoveFirst(count);
                    commitEvent.Set(true);
                }
            }

            return Math.Max(count, 0L);
        }

        /// <inheritdoc/>
        ValueTask<long> IAuditTrail.CommitAsync(long endIndex, CancellationToken token)
            => CommitAsync(endIndex, token);

        /// <inheritdoc/>
        ValueTask<long> IAuditTrail.CommitAsync(CancellationToken token)
            => CommitAsync(null, token);

        /// <inheritdoc/>
        async ValueTask<long> IAuditTrail.DropAsync(long startIndex, CancellationToken token)
        {
            long count;
            using (await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false))
            {
                if (startIndex <= commitIndex.VolatileRead())
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                count = index.VolatileRead() - startIndex + 1L;
                index.VolatileWrite(startIndex - 1L);
                log = log.RemoveLast(count);
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

        /// <inheritdoc/>
        ValueTask<long> IPersistentState.IncrementTermAsync() => new ValueTask<long>(term.IncrementAndGet());

        /// <inheritdoc/>
        Task IAuditTrail.InitializeAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        /// <inheritdoc/>
        bool IPersistentState.IsVotedFor(IRaftClusterMember? member)
        {
            var lastVote = votedFor;
            return lastVote is null || ReferenceEquals(lastVote, member);
        }

        private ValueTask<TResult> ReadCoreAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
        {
            if (endIndex > index.VolatileRead())
                throw new ArgumentOutOfRangeException(nameof(endIndex));

            var commitIndex = this.commitIndex.VolatileRead();
            var offset = startIndex - commitIndex - 1L;
            return reader.ReadAsync<EmptyLogEntry, EntryList>(new EntryList(log, endIndex - startIndex + 1, offset, lastTerm.VolatileRead()), offset >= 0 ? null : new long?(commitIndex), token);
        }

        /// <summary>
        /// Reads the range of the log entries from this storage.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader over log entries.</param>
        /// <param name="startIndex">The index of the first log entry to retrieve.</param>
        /// <param name="endIndex">The index of the last log entry to retrieve, inclusive.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The result of the transformation applied to the range of the log entries.</returns>
        public async ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return await reader.ReadAsync<EmptyLogEntry, EmptyLogEntry[]>(Array.Empty<EmptyLogEntry>(), null, token).ConfigureAwait(false);
            using (await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false))
                return await ReadCoreAsync(reader, startIndex, endIndex, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
            => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, token);

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token)
            => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, token);

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail.ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token)
            => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, token);

        /// <summary>
        /// Reads the log entries starting at the specified index to the end of the log.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader over log entries.</param>
        /// <param name="startIndex">The index of the first log entry to retrieve.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The result of the transformation applied to the range of the log entries.</returns>
        public async ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            using (await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false))
                return await ReadCoreAsync(reader, startIndex, index.VolatileRead(), token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
            => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, endIndex, token);

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token)
            => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, endIndex, token);

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail.ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token)
            => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, endIndex, token);

        /// <inheritdoc/>
        ValueTask IPersistentState.UpdateTermAsync(long value)
        {
            term.VolatileWrite(value);
            return new ValueTask();
        }

        /// <inheritdoc/>
        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember? member)
        {
            votedFor = member;
            return new ValueTask();
        }

        /// <inheritdoc/>
        Task<bool> IAuditTrail.WaitForCommitAsync(TimeSpan timeout, CancellationToken token)
            => commitEvent.WaitAsync(timeout, token);

        /// <inheritdoc/>
        Task<bool> IAuditTrail.WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => commitEvent.WaitForCommitAsync(IsCommittedPredicate, this, index, timeout, token);

        private async Task EnsureConsistency(TimeSpan timeout, CancellationToken token)
        {
            for (var timeoutTracker = new Timeout(timeout); term.VolatileRead() > lastTerm.VolatileRead(); await commitEvent.WaitAsync(timeout, token).ConfigureAwait(false))
                timeoutTracker.ThrowIfExpired(out timeout);
        }

        /// <inheritdoc/>
        Task IPersistentState.EnsureConsistencyAsync(TimeSpan timeout, CancellationToken token)
            => term.VolatileRead() <= lastTerm.VolatileRead() ? Task.CompletedTask : EnsureConsistency(timeout, token);

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
