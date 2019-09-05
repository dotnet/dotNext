using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static System.Text.Encoding;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using Threading;
    using CommitEventHandler = Replication.CommitEventHandler<IRaftLogEntry>;
    using IAuditTrail = Replication.IAuditTrail<IRaftLogEntry>;
    using ILogEntry = Replication.ILogEntry;

    /// <summary>
    /// Represents persistent audit trail which uses file system
    /// to store log entries and node state.
    /// </summary>
    /// <remarks>
    /// The layout of of the audit trail file system:
    /// <list type="table">
    /// <item>
    /// <term>node.state</term>
    /// <description>file containing internal state of Raft node</description>
    /// </item>
    /// <item>
    /// <term>&lt;partition&gt;</term>
    /// <description>file containing log partition with log records</description>
    /// </item>
    /// <item>
    /// <term>snapshot</term>
    /// <description>file containing snapshot</description>
    /// </item>
    /// </list>
    /// </remarks>
    public class PersistentState : Disposable, IPersistentState
    {
        /*
         * Log entry format:
         * 8 bytes = term
         * 8 bytes = timestamp (UTC)
         * 8 bytes = content length
         * octet stream = content
         */
        protected sealed class LogEntry : IRaftLogEntry
        {
            internal const long TermOffset = 0L;
            internal const long TimestampOffset = TermOffset + sizeof(long);
            internal const long LengthOffset = TimestampOffset + sizeof(long);

            private readonly Stream content;
            private readonly long contentOffset;
            internal readonly long Length;
            private readonly AsyncLock syncRoot;

            internal LogEntry(BinaryReader reader, AsyncLock syncRoot)
            {
                this.syncRoot = syncRoot;
                content = reader.BaseStream;
                //parse entry metadata
                Term = reader.ReadInt64();
                Timestamp = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
                Length = reader.ReadInt64();
                contentOffset = reader.BaseStream.Position;
            }

            bool ILogEntry.IsSnapshot => false;

            internal static async Task WriteAsync(IRaftLogEntry entry, BinaryWriter writer)
            {
                writer.Write(entry.Term);
                writer.Write(entry.Timestamp.UtcTicks);
                var lengthPos = writer.BaseStream.Position; //remember position of the length
                writer.Write(default(long));
                await entry.CopyToAsync(writer.BaseStream).ConfigureAwait(false);
                var length = writer.BaseStream.Position - lengthPos - sizeof(long);
                writer.BaseStream.Position = lengthPos;
                writer.Write(length);
            }

            async Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
            {
                using (await syncRoot.Acquire(token).ConfigureAwait(false))
                using (var segment = new StreamSegment(content))
                {
                    segment.SetRange(contentOffset, Length);
                    await segment.CopyToAsync(output, 1024, token).ConfigureAwait(false);
                }
            }

            async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
            {
                using (await syncRoot.Acquire(token).ConfigureAwait(false))
                using (var segment = new StreamSegment(content))
                {
                    segment.SetRange(contentOffset, Length);
                    await segment.CopyToAsync(output, false, token: token).ConfigureAwait(false);
                }
            }

            long? IDataTransferObject.Length => Length;
            bool IDataTransferObject.IsReusable => true;

            public long Term { get; }

            public DateTimeOffset Timestamp { get; }
        }

        /*
            Partition file format:
            FileName - number of partition
            OF 8 bytes = record index offset
            Allocation table:
            [8 bytes = pointer to content] X number of entries
            Payload:
            [metadata, 8 bytes = length, content] X number of entries
         */
        private sealed class Partition : FileStream
        {
            private const long AllocationTableEntrySize = sizeof(long);
            private const long IndexOffsetOffset = 0;
            private const long AllocationTableOffset = IndexOffsetOffset + sizeof(long);

            private readonly AsyncLock syncRoot;
            private readonly long payloadOffset;
            private readonly BinaryReader reader;
            private readonly BinaryWriter writer;
            internal readonly long IndexOffset;

            internal Partition(string fileName, long recordsPerPartition, AsyncLock syncRoot)
                : base(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 2048, FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous)
            {
                this.syncRoot = syncRoot;
                payloadOffset = AllocationTableOffset + AllocationTableEntrySize * recordsPerPartition;
                Capacity = recordsPerPartition;
                if (Length == 0)
                    SetLength(payloadOffset);
                reader = new BinaryReader(this, UTF8, true);
                writer = new BinaryWriter(this, UTF8, true);
                //restore index offset
                Position = IndexOffsetOffset;
                IndexOffset = reader.ReadInt64();
            }

            internal Partition(DirectoryInfo location, long recordsPerPartition, long partitionNumber, AsyncLock syncRoot)
                : this(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), recordsPerPartition, syncRoot)
            {
                Position = IndexOffsetOffset;
                writer.Write(IndexOffset = partitionNumber * recordsPerPartition);
            }

            //max number of entries
            internal long Capacity { get; }

            //current count of entries
            internal long Count
            {
                get
                {
                    var result = IndexOffset == 0L ? 1L : 0L;
                    while (result < Capacity)
                    {
                        Position = AllocationTableOffset + result * AllocationTableEntrySize;
                        if (reader.ReadInt64() == 0L)
                            break;
                        else
                            result += 1;
                    }
                    return result;
                }
            }

            internal IRaftLogEntry this[long index]
            {
                get
                {
                    var offset = IndexOffset;
                    if (index == 0L && offset == 0L)
                        return InMemoryAuditTrail.EmptyLog[0];
                    //calculate relative index
                    index -= offset;
                    //find pointer to the content
                    Position = AllocationTableOffset + index * AllocationTableEntrySize;
                    offset = reader.ReadInt64();
                    if (offset == 0L)
                        return null;
                    Position = offset;
                    return new LogEntry(reader, syncRoot);
                }
            }

            internal async Task WriteAsync(IRaftLogEntry entry, long index)
            {
                //calculate relative index
                index -= IndexOffset;
                //calculate offset of the previous entry
                long offset;
                if (index == 0L || index == 1L && IndexOffset == 0L)
                    offset = payloadOffset;
                else
                {
                    //read position of the previous entry
                    Position = AllocationTableOffset + (index - 1) * AllocationTableEntrySize;
                    offset = reader.ReadInt64();
                    Debug.Assert(offset > 0, "Previous entry doesn't exist for unknown reason");
                    //read length of the previous entry
                    Position = offset + LogEntry.LengthOffset;
                    //calculate offset to the newly entry
                    offset = reader.ReadInt64();
                    offset += Position;
                }
                //record offset into the table
                Position = offset;
                await LogEntry.WriteAsync(entry, writer).ConfigureAwait(false);
                //record new log entry to the allocation table
                Position = AllocationTableOffset + index * AllocationTableEntrySize;
                writer.Write(offset);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    reader.Dispose();
                    writer.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /*
            State file format:
            8 bytes = Term
            8 bytes = CommitIndex
            4 bytes = Node port
            4 bytes = Address Length
            octet string = IP Address
         */
        private sealed class NodeState : Disposable
        {
            internal const string FileName = "node.state";
            private const long Capacity = 1024; //1 KB
            private const long TermOffset = 0L;
            private const long CommitIndexOffset = TermOffset + sizeof(long);
            private const long PortOffset = CommitIndexOffset + sizeof(long);
            private const long AddressLengthOffset = PortOffset + sizeof(int);
            private const long AddressOffset = AddressLengthOffset + sizeof(int);
            private readonly MemoryMappedFile mappedFile;
            private readonly MemoryMappedViewAccessor stateView;
            private readonly AsyncLock syncRoot;
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
                if (length == 0)
                    votedFor = null;
                else
                {
                    var address = new byte[length];
                    stateView.ReadArray(AddressOffset, address, 0, length);
                    votedFor = new IPEndPoint(new IPAddress(address), port);
                }
            }

            internal long CommitIndex
            {
                get => stateView.ReadInt64(CommitIndexOffset);
                set => stateView.Write(CommitIndexOffset, value);
            }

            internal long Term => term.VolatileRead();

            internal async ValueTask UpdateTermAsync(long value)
            {
                using (await syncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
                {
                    stateView.Write(TermOffset, value);
                    stateView.Flush();
                    term.VolatileWrite(value);
                }
            }

            internal async ValueTask<long> IncrementTermAsync()
            {
                using (await syncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
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
                using (await syncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
                {
                    if (member is null)
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
                if (disposing)
                {
                    stateView.Dispose();
                    mappedFile.Dispose();
                    votedFor = null;
                }
                base.Dispose(disposing);
            }
        }

        private static readonly ValueFunc<long, long, long> MaxFunc = new ValueFunc<long, long, long>(Math.Max);
        private long lastIndex;
        private readonly long recordsPerPartition;
        private readonly AsyncExclusiveLock syncRoot;
        //key is the number of partition
        private readonly Dictionary<long, Partition> partitionTable;
        private readonly NodeState state;
        private readonly DirectoryInfo location;

        public PersistentState(DirectoryInfo location, long recordsPerPartition)
        {
            if (!location.Exists)
                location.Create();
            this.location = location;
            this.recordsPerPartition = recordsPerPartition;
            syncRoot = new AsyncExclusiveLock();
            partitionTable = new Dictionary<long, Partition>();
            //load all partitions from file system
            foreach (var file in location.EnumerateFiles())
                if (long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.FullName, recordsPerPartition, AsyncLock.Exclusive(syncRoot));
                    lastIndex += partition.Count;
                    partitionTable[partitionNumber] = partition;
                }
            //load node state
            state = new NodeState(Path.Combine(location.FullName, NodeState.FileName), AsyncLock.Exclusive(syncRoot));
        }

        public PersistentState(string path, long recordsPerPartition)
            : this(new DirectoryInfo(path), recordsPerPartition)
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
        public long GetLastIndex(bool committed) => committed ? state.CommitIndex : lastIndex.VolatileRead();

        private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

        private bool TryGetPartition(long recordIndex, out Partition partition)
            => partitionTable.TryGetValue(PartitionOf(recordIndex), out partition);

        private Partition GetOrCreatePartition(long recordIndex, out long partitionNumber)
        {
            partitionNumber = PartitionOf(recordIndex);
            if (!partitionTable.TryGetValue(partitionNumber, out var partition))
            {
                partition = new Partition(location, recordsPerPartition, partitionNumber, AsyncLock.Exclusive(syncRoot));
                partition.Flush();
                partitionTable.Add(partitionNumber, partition);
            }
            return partition;
        }

        private IReadOnlyList<IRaftLogEntry> GetEntries(long startIndex, long endIndex)
        {
            if (startIndex > lastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            if (endIndex < startIndex)
                return Array.Empty<LogEntry>();
            if (partitionTable.Count == 0)
                return startIndex == 0L ? InMemoryAuditTrail.EmptyLog : Array.Empty<LogEntry>();

            var result = new IRaftLogEntry[endIndex - startIndex + 1L];
            for (var i = 0L; startIndex <= endIndex; startIndex++, i++)
            {
                IRaftLogEntry entry;
                if (TryGetPartition(startIndex, out var partition) && (entry = partition[startIndex]) != null)
                    result[i] = entry;
                else
                {
                    result = result.RemoveLast(result.LongLength - i);
                    break;
                }
            }
            return result;
        }

        async Task<IReadOnlyList<IRaftLogEntry>> IAuditTrail.GetEntriesAsync(long startIndex, long? endIndex)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
                return GetEntries(startIndex, endIndex ?? lastIndex);
        }

        private async Task<long> AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex)
        {
            long partitionLow = 0, partitionHigh = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var idx = startIndex + i;
                lastIndex.AccumulateAndGet(idx, in MaxFunc);
                await GetOrCreatePartition(idx, out var partitionNumber).WriteAsync(entries[i], idx).ConfigureAwait(false);
                partitionLow = Math.Min(partitionLow, partitionNumber);
                partitionHigh = Math.Max(partitionHigh, partitionNumber);
            }
            //flush all touched partitions
            Task flushTask;
            switch(partitionHigh - partitionLow)
            {
                case 0:
                    flushTask = partitionTable[partitionLow].FlushAsync();
                    break;
                case 1:
                    flushTask = Task.WhenAll(partitionTable[partitionLow].FlushAsync(), partitionTable[partitionHigh].FlushAsync());
                    break;
                default:
                    ICollection<Task> flushTasks = new LinkedList<Task>();
                    while (partitionLow <= partitionHigh)
                        flushTasks.Add(partitionTable[partitionLow++].FlushAsync());
                    flushTask = Task.WhenAll(flushTasks);
                    break;
            }
            await flushTask.ConfigureAwait(false);
            return startIndex;
        }

        async Task<long> IAuditTrail.AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long? startIndex)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
                return await AppendAsync(entries, startIndex ?? lastIndex + 1L).ConfigureAwait(false);
        }

        /// <summary>
        /// The event that is raised when actual commit happen.
        /// </summary>
        public event CommitEventHandler Committed;

        Task<long> IAuditTrail.CommitAsync(long? endIndex)
        {
            throw new NotImplementedException();
        }

        ref readonly IRaftLogEntry IAuditTrail.First => ref InMemoryAuditTrail.EmptyLog[0];

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
            if (disposing)
            {
                syncRoot.Dispose();
                foreach (var partition in partitionTable.Values)
                    partition.Dispose();
                partitionTable.Clear();
                state.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}