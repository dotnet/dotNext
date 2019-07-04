using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Messaging;
    using Replication;
    using Threading;

    /// <summary>
    /// Represents in-memory audit trail for Raft-based cluster node.
    /// </summary>
    public sealed class InMemoryAuditTrail : AsyncReaderWriterLock, IPersistentState
    {
        private sealed class BufferedLogEntry : BinaryMessage, ILogEntry
        {
            private BufferedLogEntry(ReadOnlyMemory<byte> content, string name, ContentType type, long term)
                : base(content, name, type)
            {
                Term = term;
            }

            internal static async Task<BufferedLogEntry> CreateBufferedEntryAsync(ILogEntry entry)
            {
                ReadOnlyMemory<byte> content;
                using (var ms = new MemoryStream(1024))
                {
                    await entry.CopyToAsync(ms).ConfigureAwait(false);
                    ms.Seek(0, SeekOrigin.Begin);
                    content = ms.TryGetBuffer(out var segment)
                        ? segment
                        : new ReadOnlyMemory<byte>(ms.ToArray());
                }

                return new BufferedLogEntry(content, entry.Name, entry.Type, entry.Term);
            }

            public long Term { get; }
        }

        private sealed class InitialLogEntry : ILogEntry
        {
            string IMessage.Name => "NOP";
            long? IMessage.Length => 0L;
            Task IMessage.CopyToAsync(Stream output) => Task.CompletedTask;

            ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask();

            public ContentType Type { get; } = new ContentType(MediaTypeNames.Application.Octet);
            long ILogEntry.Term => 0L;

            bool IMessage.IsReusable => true;
        }

        private sealed class CommitEventExecutor
        {
            private readonly long startIndex, count;

            internal CommitEventExecutor(long startIndex, long count)
            {
                this.startIndex = startIndex;
                this.count = count;
            }

            private void Invoke(InMemoryAuditTrail auditTrail) => auditTrail?.Committed?.Invoke(auditTrail, startIndex, count);

            private void Invoke(object auditTrail) => Invoke(auditTrail as InMemoryAuditTrail);

            public static implicit operator WaitCallback(CommitEventExecutor executor) => executor is null ? default(WaitCallback) : executor.Invoke;
        }

        private static readonly ILogEntry First = new InitialLogEntry();
        private static readonly ILogEntry[] EmptyLog = { First };

        private long commitIndex;
        private volatile ILogEntry[] log;
         
        private long term;
        private volatile IRaftClusterMember votedFor;

        /// <summary>
        /// Initializes a new audit trail with empty log.
        /// </summary>
        public InMemoryAuditTrail() => log = EmptyLog;

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
            => committed ? commitIndex : Math.Max(0, log.LongLength - 1L);

        private IReadOnlyList<ILogEntry> GetEntries(long startIndex, long endIndex)
            => log.Slice(startIndex, endIndex - startIndex + 1);

        async ValueTask<IReadOnlyList<ILogEntry>> IAuditTrail<ILogEntry>.GetEntriesAsync(long startIndex, long? endIndex)
        {
            using (await this.AcquireReadLockAsync(CancellationToken.None).ConfigureAwait(false))
                return GetEntries(startIndex, endIndex ?? GetLastIndex(false));
        }


        private long Append(ILogEntry[] entries, long? startIndex)
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
            entries.CopyTo(newLog, log.LongLength);
            log = newLog;
            return result;
        }

        async ValueTask<long> IAuditTrail<ILogEntry>.AppendAsync(IReadOnlyList<ILogEntry> entries, long? startIndex)
        {
            if(entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await this.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var bufferedEntries = new ILogEntry[entries.Count];
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    bufferedEntries[i] = entry.IsReusable
                        ? entry
                        : await BufferedLogEntry.CreateBufferedEntryAsync(entry).ConfigureAwait(false);
                }
                return Append(bufferedEntries, startIndex);
            }
        }

        /// <summary>
        /// The event that is raised when actual commit happen.
        /// </summary>
        public event CommitEventHandler<ILogEntry> Committed;

        private long Commit(long startIndex, long count)
        {
            count = Math.Min(log.LongLength - startIndex, count);
            if(count > 0L)
            {
                commitIndex = startIndex + count - 1;
                //TODO: Should be replaced with typed QueueUserWorkItem in .NET Standard 2.1
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
                return count > 0L ? Commit(startIndex, count) : 0L;
            }
        }

        ref readonly ILogEntry IAuditTrail<ILogEntry>.First => ref First;
    }
}