using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Runtime.CompilerServices;
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

            ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask(Task.CompletedTask);

            public ContentType Type { get; } = new ContentType(MediaTypeNames.Application.Octet);
            long ILogEntry.Term => 0L;

            bool IMessage.IsReusable => true;
        }

        private static readonly ILogEntry First = new InitialLogEntry();
        private static readonly ILogEntry[] EmptyLog = { First };

        private long commitIndex, offset;
        private volatile ILogEntry[] log;

        private long term;
        private volatile IRaftClusterMember votedFor;

        /// <summary>
        /// Initializes a new audit trail with empty log.
        /// </summary>
        public InMemoryAuditTrail() => log = EmptyLog;
        
        //converts record index into array index
        private long this[long recordIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => recordIndex - offset.VolatileRead();
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
            return new ValueTask(Task.CompletedTask);
        }

        ValueTask<long> IPersistentState.IncrementTermAsync() => new ValueTask<long>(term.IncrementAndGet());

        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember member)
        {
            votedFor = member;
            return new ValueTask(Task.CompletedTask);
        }

        /// <summary>
        /// Gets index of the committed or last log entry.
        /// </summary>
        /// <param name="committed"><see langword="true"/> to get the index of highest log entry known to be committed; <see langword="false"/> to get the index of the last log entry.</param>
        /// <returns>The index of the log entry.</returns>
        public long GetLastIndex(bool committed)
            => committed ? commitIndex.VolatileRead() : (Math.Max(0, log.LongLength - 1L) + offset.VolatileRead());

        private IReadOnlyList<ILogEntry> GetEntries(long startIndex, long endIndex)
            => log.Slice(this[startIndex], endIndex - startIndex + 1);

        async ValueTask<IReadOnlyList<ILogEntry>> IAuditTrail<ILogEntry>.GetEntriesAsync(long startIndex, long? endIndex)
        {
            using (await this.AcquireReadLockAsync(CancellationToken.None).ConfigureAwait(false))
                return GetEntries(startIndex, endIndex ?? GetLastIndex(false));
        }

        private long Append(ILogEntry[] entries, long? startIndex)
        {
            long result;
            if (startIndex.HasValue)
            {
                result = startIndex.Value;
                log = log.RemoveLast(log.LongLength - this[result]);
            }
            else
                result = log.LongLength + offset;
            var newLog = new ILogEntry[entries.Length + log.LongLength];
            Array.Copy(log, newLog, log.LongLength);
            entries.CopyTo(newLog, log.LongLength);
            log = newLog;
            return result;
        }

        async ValueTask<long> IAuditTrail<ILogEntry>.AppendAsync(IReadOnlyList<ILogEntry> entries, long? startIndex)
        {
            if (entries.Count == 0)
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

        private Task OnCommmitted(long startIndex, long count)
        {
            ICollection<Task> tasks = new LinkedList<Task>();
            foreach (CommitEventHandler<ILogEntry> handler in Committed?.GetInvocationList() ?? Array.Empty<CommitEventHandler<ILogEntry>>())
                tasks.Add(handler(this, startIndex, count));
            return Task.WhenAll(tasks);
        }

        async ValueTask<long> IAuditTrail<ILogEntry>.CommitAsync(long startIndex, long? endIndex)
        {
            using (await this.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                startIndex = Math.Max(commitIndex.VolatileRead(), startIndex);
                var count = endIndex.HasValue ?
                    Math.Min(log.LongLength - this[startIndex], this[endIndex.Value - startIndex + 1L]) :
                    log.LongLength - this[startIndex];
                if (count > 0L)
                {
                    commitIndex.VolatileWrite(startIndex + count - 1);
                    await OnCommmitted(startIndex, count).ConfigureAwait(false);
                }
                return count;
            }
        }

        /// <summary>
        /// Performs log compaction.
        /// </summary>
        /// <returns>The number of removed entries.</returns>
        public async ValueTask<long> ForceCompactionAsync()
        {
            using(await this.AcquireWriteLockAsync(CancellationToken.None))
            {
                var ci = this[commitIndex.VolatileRead()];
                //remove all records up to commitIndex inclusive
                var newLog = new ILogEntry[log.LongLength - ci];
                newLog[0] = First;
                offset.Add(ci);
                Array.Copy(log, ci + 1, newLog, 1L, newLog.LongLength - 1L);
                log = newLog;
                return ci;
            }
        }

        ref readonly ILogEntry IAuditTrail<ILogEntry>.First => ref First;
    }
}