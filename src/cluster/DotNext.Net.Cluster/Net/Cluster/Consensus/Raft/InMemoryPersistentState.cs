using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IMessage = Messaging.IMessage;
    using Replication;
    using Threading;

    internal sealed class InMemoryPersistentState : AsyncReaderWriterLock, IPersistentState
    {
        private sealed class InitialLogEntry : ILogEntry
        {
            string IMessage.Name => "NOP";
            long? IMessage.Length => 0L;
            Task IMessage.CopyToAsync(Stream output) => Task.CompletedTask;

            ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask();

            public ContentType Type { get; } = new ContentType(MediaTypeNames.Application.Octet);
            long ILogEntry.Term => 0L;
        }

        private sealed class CommitEventExecutor
        {
            private readonly long startIndex, count;

            internal CommitEventExecutor(long startIndex, long count)
            {
                this.startIndex = startIndex;
                this.count = count;
            }

            private void Invoke(InMemoryPersistentState auditTrail) => auditTrail?.Committed?.Invoke(auditTrail, startIndex, count);

            private void Invoke(object auditTrail) => Invoke(auditTrail as InMemoryPersistentState);

            public static implicit operator WaitCallback(CommitEventExecutor executor) => executor is null ? default(WaitCallback) : executor.Invoke;
        }

        private static readonly ILogEntry First = new InitialLogEntry();
        private static readonly ILogEntry[] EmptyLog = { First };

        private long commitIndex;
        private volatile ILogEntry[] log;
         
        private long term;
        private volatile IRaftClusterMember votedFor;

        internal InMemoryPersistentState() => log = EmptyLog;

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

        public long GetLastIndex(bool committed)
            => committed ? commitIndex : Math.Max(0, log.LongLength - 1L);

        private ReadOnlyMemory<ILogEntry> GetEntries(long startIndex, long endIndex)
            => log.Slice(startIndex, endIndex - startIndex + 1);

        async ValueTask<ReadOnlyMemory<ILogEntry>> IAuditTrail<ILogEntry>.GetEntriesAsync(long startIndex, long? endIndex)
        {
            using (await this.AcquireReadLockAsync(CancellationToken.None).ConfigureAwait(false))
                return GetEntries(startIndex, endIndex ?? GetLastIndex(false));
        }

        private long Append(ReadOnlyMemory<ILogEntry> entries, long? startIndex)
        {
            long result;
            if(startIndex.HasValue)
            {
                result = startIndex.Value;
                log = log.RemoveLast(log.LongLength - result);
            }
            else
                result = log.LongLength;
            var newLog = new ILogEntry[entries.Length + log.LongLength];
            Array.Copy(log, newLog, log.LongLength);
            entries.CopyTo(new Memory<ILogEntry>(newLog, log.Length, entries.Length));
            return result;
        }

        async ValueTask<long> IAuditTrail<ILogEntry>.AppendAsync(ReadOnlyMemory<ILogEntry> entries, long? startIndex)
        {
            if(entries.IsEmpty)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await this.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                return Append(entries, startIndex);
        }

        public event CommitEventHandler<ILogEntry> Committed;

        private long Commit(long startIndex, long count)
        {
            count = Math.Min(log.LongLength - startIndex, count);
            if(count > 0L)
            {
                commitIndex = startIndex + count;
                ThreadPool.QueueUserWorkItem(new CommitEventExecutor(startIndex, count), this);
            }
            return count;
        }

        async ValueTask<long> IAuditTrail<ILogEntry>.CommitAsync(long startIndex, long? endIndex)
        {
            using (await this.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                startIndex = Math.Max(commitIndex, startIndex);
                var count = (endIndex ?? GetLastIndex(false)) - startIndex + 1L;
                if(count <= 0L)
                    return 0L;
                return Commit(startIndex, count);
            }
        }

        ref readonly ILogEntry IAuditTrail<ILogEntry>.First => ref First;
    }
}