using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Threading;
using BoxedClusterMemberId = Runtime.BoxedValue<ClusterMemberId>;

/// <summary>
/// Represents lightweight Raft node state that is suitable for distributed consensus only.
/// </summary>
/// <remarks>
/// The actual state doesn't persist on disk and exists only in memory. Moreover, this implementation
/// cannot append non-empty log entries.
/// </remarks>
public sealed class ConsensusOnlyState : Disposable, IPersistentState
{
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
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((ulong)index, (ulong)count, nameof(index));

                EmptyLogEntry result;
                if (offset >= 0L)
                {
                    result = new() { Term = terms[index + offset] };
                }
                else if (index is 0)
                {
                    result = new() { Term = snapshotTerm, IsSnapshot = true };
                }
                else
                {
                    result = new() { Term = terms[index - 1L] };
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
                yield return new() { Term = snapshotTerm, IsSnapshot = true };
            for (var i = 0L; i < count + offset; i++)
                yield return new() { Term = terms[i] };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct CommitChecker : ISupplier<bool>
    {
        private readonly ConsensusOnlyState state;
        private readonly long index;

        internal CommitChecker(ConsensusOnlyState state, long index)
        {
            Debug.Assert(state is not null);

            this.state = state;
            this.index = index;
        }

        bool ISupplier<bool>.Invoke()
            => index <= Atomic.Read(in state.commitIndex);
    }

    private readonly AsyncReaderWriterLock syncRoot = new();
    private readonly AsyncTrigger commitEvent = new();
    private long term, commitIndex, lastTerm, index;

    // boxed ClusterMemberId or null if there is not last vote stored
    private volatile BoxedClusterMemberId? lastVote;
    private volatile long[] log = [];    // log of uncommitted entries

    /// <inheritdoc cref="IPersistentState.Term"/>
    public long Term
    {
        get => Atomic.Read(in term);
        private set => Atomic.Write(ref term, value);
    }

    /// <inheritdoc/>
    bool IAuditTrail.IsLogEntryLengthAlwaysPresented => true;

    private void Append(long[] terms, long startIndex)
    {
        log = Concat(log, terms, startIndex - LastCommittedEntryIndex - 1L);
        LastEntryIndex = startIndex + terms.LongLength - 1L;

        static long[] Concat(long[] left, long[] right, long startIndex)
        {
            var result = new long[startIndex + right.LongLength];
            Array.Copy(left, result, startIndex);
            Array.Copy(right, 0L, result, startIndex, right.Length);
            return result;
        }
    }

    private async ValueTask<long> AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long? startIndex, bool skipCommitted, CancellationToken token)
        where TEntryImpl : IRaftLogEntry
    {
        long skip;
        if (startIndex is null)
            startIndex = index + 1L;
        else if (startIndex.GetValueOrDefault() > index + 1L)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        if (skipCommitted)
        {
            skip = Math.Max(0, LastCommittedEntryIndex - startIndex.GetValueOrDefault() + 1L);
            startIndex += skip;
        }
        else if (startIndex.GetValueOrDefault() <= LastCommittedEntryIndex)
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
                newEntries[i] = entries.Current is { IsSnapshot: false } entry
                    ? entry.Term
                    : throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);
            }

            // now concat existing array of terms
            Append(newEntries, startIndex.GetValueOrDefault());
        }

        return startIndex.GetValueOrDefault();
    }

    /// <inheritdoc/>
    async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long startIndex, bool skipCommitted, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);

        await syncRoot.EnterWriteLockAsync(token).ConfigureAwait(false);
        try
        {
            await AppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release();
        }
    }

    /// <inheritdoc/>
    async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(TEntry entry, long startIndex, CancellationToken token)
    {
        await syncRoot.EnterWriteLockAsync(token).ConfigureAwait(false);
        try
        {
            if (startIndex <= LastCommittedEntryIndex)
            {
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            }
            else if (entry.IsSnapshot)
            {
                Atomic.Write(ref lastTerm, entry.Term);
                LastCommittedEntryIndex = LastEntryIndex = startIndex;
                log = [];
                commitEvent.Signal(resumeAll: true);
            }
            else if (startIndex > LastEntryIndex + 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            else
            {
                long[] singleEntryTerm = { entry.Term };
                Append(singleEntryTerm, startIndex);
            }
        }
        finally
        {
            syncRoot.Release();
        }
    }

    /// <inheritdoc/>
    async ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(TEntry entry, CancellationToken token)
    {
        await syncRoot.EnterWriteLockAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            return await AppendAsync(LogEntryProducer<TEntry>.Of(entry), null, false, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release();
        }
    }

    /// <inheritdoc cref="IAuditTrail.CommitAsync(long, CancellationToken)"/>
    public async ValueTask<long> CommitAsync(long endIndex, CancellationToken token)
    {
        long count;
        await syncRoot.EnterWriteLockAsync(token).ConfigureAwait(false);
        try
        {
            var startIndex = LastCommittedEntryIndex + 1L;
            count = endIndex - startIndex + 1L;
            if (count > 0)
            {
                LastCommittedEntryIndex = startIndex + count - 1;
                Atomic.Write(ref lastTerm, log[count - 1]);

                // count indicates how many elements should be removed from log
                log = log[(int)count..];
                commitEvent.Signal(resumeAll: true);
            }
        }
        finally
        {
            syncRoot.Release();
        }

        return Math.Max(count, 0L);
    }

    /// <summary>
    /// Gets the index of the last committed log entry.
    /// </summary>
    public long LastCommittedEntryIndex
    {
        get => Atomic.Read(in commitIndex);
        private set => Atomic.Write(ref commitIndex, value);
    }

    /// <summary>
    /// Gets the index of the last uncommitted log entry.
    /// </summary>
    public long LastEntryIndex
    {
        get => Atomic.Read(in index);
        private set => Atomic.Write(ref index, value);
    }

    /// <inheritdoc/>
    ValueTask<long> IPersistentState.IncrementTermAsync(ClusterMemberId member, CancellationToken token)
    {
        lastVote = BoxedClusterMemberId.Box(member);
        return token.IsCancellationRequested
            ? ValueTask.FromCanceled<long>(token)
            : ValueTask.FromResult(Interlocked.Increment(ref term));
    }

    /// <inheritdoc/>
    Task IAuditTrail.InitializeAsync(CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

    /// <inheritdoc/>
    bool IPersistentState.IsVotedFor(in ClusterMemberId id) => IPersistentState.IsVotedFor(lastVote, in id);

    private ValueTask<TResult> ReadCoreAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
    {
        if (endIndex > LastEntryIndex)
            return ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(endIndex)));

        var commitIndex = LastCommittedEntryIndex;
        var offset = startIndex - commitIndex - 1L;
        return reader.ReadAsync<EmptyLogEntry, EntryList>(new EntryList(log, endIndex - startIndex + 1, offset, Atomic.Read(in lastTerm)), offset >= 0 ? null : new long?(commitIndex), token);
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
    public async ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(endIndex);

        if (endIndex < startIndex)
            return await reader.ReadAsync<EmptyLogEntry, EmptyLogEntry[]>([], null, token).ConfigureAwait(false);

        await syncRoot.EnterReadLockAsync(token).ConfigureAwait(false);
        try
        {
            return await ReadCoreAsync(reader, startIndex, endIndex, token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release();
        }
    }

    /// <summary>
    /// Reads the log entries starting at the specified index to the end of the log.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="reader">The reader over log entries.</param>
    /// <param name="startIndex">The index of the first log entry to retrieve.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of the transformation applied to the range of the log entries.</returns>
    public async ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);

        await syncRoot.EnterReadLockAsync(token).ConfigureAwait(false);
        try
        {
            return await ReadCoreAsync(reader, startIndex, LastEntryIndex, token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release();
        }
    }

    /// <inheritdoc/>
    ValueTask IPersistentState.UpdateTermAsync(long value, bool resetLastVote, CancellationToken token)
    {
        Term = value;
        if (resetLastVote)
            lastVote = null;

        return token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    ValueTask IPersistentState.UpdateVotedForAsync(ClusterMemberId id, CancellationToken token)
    {
        lastVote = BoxedClusterMemberId.Box(id);
        return token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    ValueTask IAuditTrail.WaitForApplyAsync(CancellationToken token)
        => commitEvent.WaitAsync(token);

    /// <inheritdoc/>
    ValueTask IAuditTrail.WaitForApplyAsync(long index, CancellationToken token)
        => commitEvent.SpinWaitAsync(new CommitChecker(this, index), token);

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