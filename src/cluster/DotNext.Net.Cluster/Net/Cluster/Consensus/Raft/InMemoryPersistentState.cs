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

        private long commitIndex;
        private volatile ILogEntry[] log;
        private readonly ILogEntry first;
        private long term;
        private volatile IRaftClusterMember votedFor;

        internal InMemoryPersistentState(CancellationToken token)
        {
            first = new InitialLogEntry();
            log = Array.Empty<ILogEntry>();
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

        public long GetLastIndex(bool committed)
            => committed ? commitIndex : Math.Max(0, log.LongLength - 1L);

        private IReadOnlyList<ILogEntry> GetEntriesAsync(long startIndex, long endIndex)
        {

        }

        async ValueTask<IReadOnlyList<ILogEntry>> IAuditTrail<ILogEntry>.GetEntriesAsync(long startIndex, long? endIndex)
        {
            using (await this.AcquireReadLock(CancellationToken.None).ConfigureAwait(false))
                return GetEntriesAsync(startIndex, endIndex ?? GetLastIndex(false));
        }

        public ValueTask<long> DeleteAsync(long startIndex, long? endIndex = null)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<(long FirstIndex, long LastIndex)> PrepareAsync(IEnumerable<ILogEntry> entries)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<long> CommitAsync(long startIndex, long? endIndex = null)
        {
            throw new System.NotImplementedException();
        }

        ref readonly ILogEntry IAuditTrail<ILogEntry>.First => first;
    }
}