using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using static System.Text.Encoding;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Messaging;
    using Threading;
    using CommitEventHandler = Replication.CommitEventHandler<ILogEntry>;
    using IAuditTrail = Replication.IAuditTrail<ILogEntry>;

    public sealed class PersistentState : Disposable, IPersistentState
    {
        private sealed class LogEntry : ILogEntry
        {
            private readonly MemoryMappedFile mappedFile;
            private readonly long contentOffset;
            internal readonly long Length;

            internal LogEntry(MemoryMappedFile mappedFile, long offset, long maxRecordSize)
            {
                this.mappedFile = mappedFile;
                using(var reader = new BinaryReader(mappedFile.CreateViewStream(offset, maxRecordSize, MemoryMappedFileAccess.Read), UTF8, false))
                {
                    Name = reader.ReadString();
                    Type = new ContentType(reader.ReadString());
                    Term = reader.ReadInt64();
                    Length = reader.ReadInt64();
                    contentOffset = reader.BaseStream.Position + offset;
                }
            }

            async Task IMessage.CopyToAsync(Stream output)
            {
                using(var content = mappedFile.CreateViewStream(contentOffset, Length, MemoryMappedFileAccess.Read))
                    await content.CopyToAsync(output).ConfigureAwait(false);
            }
            
            async ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token)
            {
                using(var content = mappedFile.CreateViewStream(contentOffset, Length, MemoryMappedFileAccess.Read))
                    await StreamMessage.CopyToAsync(content, output, token, false).ConfigureAwait(false);
            }

            long? IMessage.Length => Length;
            bool IMessage.IsReusable => true;

            public string Name { get; }

            public ContentType Type { get; }

            public long Term { get; }
        }

        /*
            Partition file format:
            FileName - number of partition
            N  8 bytes = max number of entries
            OF 8 bytes = record index offset
            C  8 bytes = number of committed entries
            Allocation table:
            [8 bytes = pointer to the entry content] X N
            Payload:
            [metadata, length = 8 bytes, content] X N
         */
        private sealed class Partition : Disposable
        {
            private const long AllocationTableEntrySize = sizeof(long);
            private const long MaxNumberOfEntriesOffset = 0L;
            private const long RecordIndexOffsetOffset = MaxNumberOfEntriesOffset + sizeof(long);
            private const long CommittedEntriesOffset = RecordIndexOffsetOffset + sizeof(long);
            private const long AllocationTableOffset = CommittedEntriesOffset + sizeof(long);

            private readonly MemoryMappedFile mappedFile;
            private readonly MemoryMappedViewAccessor headersView;
            private readonly long maxRecordSize;

            internal Partition(string fileName, long recordsPerPartition, long maxRecordSize)
            {
                Capacity = recordsPerPartition;
                this.maxRecordSize = maxRecordSize;
                var preambleSize = sizeof(long) * 3 + AllocationTableEntrySize * recordsPerPartition;
                mappedFile = MemoryMappedFile.CreateFromFile(fileName, FileMode.OpenOrCreate, null, preambleSize + recordsPerPartition * maxRecordSize, MemoryMappedFileAccess.ReadWrite);
                headersView = mappedFile.CreateViewAccessor(0L, preambleSize, MemoryMappedFileAccess.ReadWrite); 
                headersView.Write(MaxNumberOfEntriesOffset, recordsPerPartition);
            }

            internal long RecordsPerPartition => headersView.ReadInt64(MaxNumberOfEntriesOffset);

            internal long IndexOffset
            {
                get => headersView.ReadInt64(RecordIndexOffsetOffset);
                set => headersView.Write(RecordIndexOffsetOffset, value);
            }

            internal long CommittedEntries
            {
                get => headersView.ReadInt64(CommittedEntriesOffset);
                set => headersView.Write(CommittedEntries, value);
            }

            //max number of entries
            internal long Capacity { get; }

            //current count of entries
            internal long Count
            {
                get
                {
                    var result = IndexOffset == 0L ? 1L : 0L;
                    while(result < Capacity)
                    {
                        var offset = AllocationTableOffset + result * AllocationTableEntrySize;
                        if(headersView.ReadInt64(offset) == 0L)
                            break;
                        result += 1L;
                    }
                    return result;
                }
            }

            internal void Flush() => headersView.Flush();

            internal ILogEntry this[long index]
            {
                get
                {
                    var offset = IndexOffset;
                    if(index == 0L && offset == 0L)
                        return First;
                    //find offset to the table entry
                    offset = AllocationTableOffset + (index - offset) * AllocationTableEntrySize;
                    //offset to the entry content
                    offset = headersView.ReadInt64(offset);
                    return index == 0L ? null : new LogEntry(mappedFile, offset, maxRecordSize);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if(disposing)
                {
                    headersView.Dispose();
                    mappedFile.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /*
            State file format:
            8 bytes = Term
            4 bytes = Node port
            4 bytes = Address Length
            octet string = IP Address
         */
        private sealed class NodeState : Disposable
        {
            internal const string FileName = ".state";
            private const long Capacity = 1024; //1 KB
            private const long TermOffset = 0L;
            private const long PortOffset = TermOffset + sizeof(long);
            private const long AddressLengthOffset = PortOffset + sizeof(int);
            private const long AddressOffset = AddressLengthOffset + sizeof(int);
            private readonly MemoryMappedFile mappedFile;
            private readonly MemoryMappedViewAccessor stateView;
            private AsyncLock syncRoot; //node state has separated 
            private volatile IPEndPoint votedFor;
            private long term;  //volatile

            internal NodeState(string fileName, AsyncLock writeLock)
            {
                mappedFile = MemoryMappedFile.CreateFromFile(fileName, FileMode.OpenOrCreate, null, Capacity, MemoryMappedFileAccess.ReadWrite);
                syncRoot = writeLock;
                stateView = mappedFile.CreateViewAccessor();
                term = stateView.ReadInt64(TermOffset);
                var port = stateView.ReadInt32(PortOffset);
                var length = stateView.ReadInt32(AddressLengthOffset);
                if(length == 0)
                    votedFor = null;
                else
                {
                    var address = new byte[length];
                    stateView.ReadArray(AddressOffset, address, 0, length);
                    votedFor = new IPEndPoint(new IPAddress(address), port);
                }
            }

            internal long Term => term.VolatileRead();

            internal async ValueTask UpdateTermAsync(long value)
            {
                using(await syncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
                {
                    stateView.Write(TermOffset, value);
                    stateView.Flush();
                    term.VolatileWrite(value);
                }
            }

            internal async ValueTask<long> IncrementTermAsync()
            {
                using(await syncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
                {
                    var result = term.IncrementAndGet();
                    stateView.Write(TermOffset, result);
                    stateView.Flush();
                    return result;
                }
            }

            internal bool IsVotedFor(IPEndPoint member)
            {
                var lastVote = votedFor;
                return lastVote is null || Equals(lastVote, member);
            }

            internal async ValueTask UpdateVotedForAsync(IPEndPoint member)
            {
                using(await syncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
                {
                    if(member is null)
                    {
                        stateView.Write(PortOffset, 0);
                        stateView.Write(AddressLengthOffset, 0);
                    }
                    else
                    {
                        stateView.Write(PortOffset, member.Port);
                        var address = member.Address.GetAddressBytes();
                        stateView.Write(AddressLengthOffset, address.Length);
                        stateView.WriteArray(AddressOffset, address, 0, address.Length);
                    }
                    stateView.Flush();
                    votedFor = member;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if(disposing)
                {
                    stateView.Dispose();
                    mappedFile.Dispose();
                    syncRoot.Dispose();
                    votedFor = null;
                }
                base.Dispose(disposing);
            }
        }

        private long commitIndex, lastIndex;
        private readonly long recordsPerPartition;
        private readonly AsyncReaderWriterLock syncRoot;
        //key is the number of partition
        private readonly Dictionary<long, Partition> partitionTable;
        private readonly NodeState state;

        public PersistentState(DirectoryInfo location, long recordsPerPartition, long maxRecordSize)
        {
            if(!location.Exists)
                location.Create();
            this.recordsPerPartition = recordsPerPartition;
            syncRoot = new AsyncReaderWriterLock();
            partitionTable = new Dictionary<long, Partition>();
            //load all partitions from file system
            foreach(var file in location.EnumerateFiles())
                if(long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.FullName, recordsPerPartition, maxRecordSize);
                    commitIndex += partition.CommittedEntries;
                    lastIndex += partition.Count;
                    partitionTable[partitionNumber] = partition;
                }
            //load node state
            state = new NodeState(Path.Combine(location.FullName, NodeState.FileName), AsyncLock.WriteLock(syncRoot));
        }

        public PersistentState(string path, long recordsPerPartition, long maxRecordSize)
            : this(new DirectoryInfo(path), recordsPerPartition, maxRecordSize)
        {
        }

        /// <summary>
        /// Gets index of the committed or last log entry.
        /// </summary>
        /// <remarks>
        /// This method is synchronous because returning value should be cached and updated in memory by implementing class.
        /// </remarks>
        /// <param name="committed"><see langword="true"/> to get the index of highest log entry known to be committed; <see langword="false"/> to get the index of the last log entry.</param>
        /// <returns>The index of the log entry.</returns>
        public long GetLastIndex(bool committed) => committed ? commitIndex.VolatileRead() : lastIndex.VolatileRead();

        private bool TryGetPartition(long recordIndex, out Partition partition)
            => partitionTable.TryGetValue(recordIndex / recordsPerPartition, out partition);

        private IReadOnlyList<ILogEntry> GetEntries(long startIndex, long endIndex)
        {
            if(endIndex < startIndex)
                return Array.Empty<LogEntry>();
            if(partitionTable.Count == 0)
                return startIndex == 0L ? InMemoryAuditTrail.EmptyLog : Array.Empty<LogEntry>();

            var result = new List<ILogEntry>();
            for(;startIndex <= endIndex; startIndex += 1L)
                if(TryGetPartition(startIndex, out var partition))
                {
                    var entry = partition[startIndex];
                    if(entry is null)
                        break;
                    result.Add(entry);
                }
            return result;
        }
        
        async ValueTask<IReadOnlyList<ILogEntry>> IAuditTrail.GetEntriesAsync(long startIndex, long? endIndex)
        {
            using(await syncRoot.AcquireReadLockAsync(CancellationToken.None))
                return GetEntries(startIndex, endIndex ?? lastIndex);
        }

        ValueTask<long> IAuditTrail.AppendAsync(IReadOnlyList<ILogEntry> entries, long? startIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The event that is raised when actual commit happen.
        /// </summary>
        public event CommitEventHandler Committed;

        ValueTask<long> IAuditTrail.CommitAsync(long? endIndex)
        {
            throw new NotImplementedException();
        }

        private static ref readonly ILogEntry First => ref InMemoryAuditTrail.EmptyLog[0];

        ref readonly ILogEntry IAuditTrail.First => ref First;

        /// <summary>
        /// Forces log compaction.
        /// </summary>
        /// <remarks>
        /// Log compaction allows to remove committed log entries from the log and reduce its size.
        /// </remarks>
        /// <returns>The number of removed entries.</returns>
        public ValueTask<long> ForceCompactionAsync()
        {
            throw new NotImplementedException();
        }

        bool IPersistentState.IsVotedFor(IRaftClusterMember member) => state.IsVotedFor(member?.Endpoint);

        long IPersistentState.Term => state.Term;

        ValueTask<long> IPersistentState.IncrementTermAsync() => state.IncrementTermAsync();

        ValueTask IPersistentState.UpdateTermAsync(long term) => state.UpdateTermAsync(term);

        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember member) => state.UpdateVotedForAsync(member?.Endpoint);

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                syncRoot.Dispose();
                foreach(var partition in partitionTable.Values)
                    partition.Dispose();
                partitionTable.Clear();
                state.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}