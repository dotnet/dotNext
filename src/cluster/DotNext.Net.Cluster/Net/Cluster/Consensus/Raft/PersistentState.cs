using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;
    using Replication;
    using Threading;

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
    public class PersistentState : AsyncReaderWriterLock, IPersistentState
    {
        /*
         * Log entry format:
         * 8 bytes = term
         * 8 bytes = timestamp (UTC)
         * 8 bytes = content length
         * octet stream = content
         */
         /// <summary>
         /// Represents persistent log entry.
         /// </summary>
        protected sealed class LogEntry : IRaftLogEntry
        {
            internal const long TermOffset = 0L;
            internal const long TimestampOffset = TermOffset + sizeof(long);
            internal const long LengthOffset = TimestampOffset + sizeof(long);

            private readonly StreamSegment content;
            private readonly long contentOffset;

            internal LogEntry(BinaryReader reader, StreamSegment cachedContent)
            {
                //parse entry metadata
                Term = reader.ReadInt64();
                Timestamp = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
                Length = reader.ReadInt64();
                contentOffset = reader.BaseStream.Position;
                content = cachedContent;
            }

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length { get; }

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

            /// <summary>
            /// Reads the number of bytes using the pre-allocated buffer.
            /// </summary>
            /// <remarks>
            /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
            /// </remarks>
            /// <param name="buffer">The buffer that is allocated by the caller.</param>
            /// <param name="count">The number of bytes to read.</param>
            /// <returns>The span of bytes representing buffer segment.</returns>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the length of <paramref name="buffer"/>.</exception>
            /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
            public ReadOnlySpan<byte> Read(byte[] buffer, int count)
            {
                if (count == 0)
                    return default;
                if (count > buffer.LongLength)
                    throw new ArgumentOutOfRangeException(nameof(count));
                var bytesRead = 0;
                do
                {
                    var n = content.Read(buffer, bytesRead, count - bytesRead);
                    if (n == 0)
                        throw new EndOfStreamException();
                    bytesRead += n;
                } while (bytesRead < count);
                return new ReadOnlySpan<byte>(buffer, 0, bytesRead);
            }

            /// <summary>
            /// Reads asynchronously the number of bytes using the pre-allocated buffer.
            /// </summary>
            /// <remarks>
            /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
            /// </remarks>
            /// <param name="buffer">The buffer that is allocated by the caller.</param>
            /// <param name="count">The number of bytes to read.</param>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <returns>The span of bytes representing buffer segment.</returns>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the length of <paramref name="buffer"/>.</exception>
            /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
            public async Task<ReadOnlyMemory<byte>> ReadAsync(byte[] buffer, int count, CancellationToken token = default)
            {
                if (count == 0)
                    return default;
                if (count > buffer.LongLength)
                    throw new ArgumentOutOfRangeException(nameof(count));
                var bytesRead = 0;
                do
                {
                    var n = await content.ReadAsync(buffer, bytesRead, count - bytesRead, token).ConfigureAwait(false);
                    if (n == 0)
                        throw new EndOfStreamException();
                    bytesRead += n;
                } while (bytesRead < count);
                return new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
            }

            /// <summary>
            /// Reads the string using the specified encoding.
            /// </summary>
            /// <remarks>
            /// The characters should be prefixed with the length in the underlying stream.
            /// </remarks>
            /// <param name="buffer">The buffer that is allocated by the caller.</param>
            /// <param name="length">The length of the string.</param>
            /// <param name="encoding">The encoding of the characters.</param>
            /// <param name="charBufferSize">The size of the temporary buffer used to store portion of the string, in bytes.</param>
            /// <returns>The string decoded from the log entry content stream.</returns>
            public unsafe string ReadString(byte[] buffer, int length, Encoding encoding, int charBufferSize = 512)
            {
                //TODO: Should be rewritten for .NET Standard 2.1
                var maxCharBytesSize = Math.Min(charBufferSize, buffer.Length);
                var maxCharsSize = encoding.GetMaxCharCount(maxCharBytesSize);
                var charBuffer = stackalloc char[maxCharsSize];
                var sb = default(StringBuilder);
                int currentPos = 0;
                do
                {
                    var readLength = Math.Min(length - currentPos, maxCharBytesSize);
                    var n = content.Read(buffer, 0, readLength);
                    if (n == 0)
                        throw new EndOfStreamException();
                    int charsRead;
                    fixed (byte* rb = buffer)
                        charsRead = encoding.GetChars(rb, n, charBuffer, maxCharsSize);
                    if (currentPos == 0 && n == length)
                        return new string(charBuffer, 0, charsRead);
                    if (sb is null)
                        sb = new StringBuilder(length);
                    sb.Append(charBuffer, charsRead);
                    currentPos += n;
                }
                while (currentPos < length);
                return sb.ToString();
            }

            /// <summary>
            /// Reads the string asynchronously using the specified encoding.
            /// </summary>
            /// <remarks>
            /// The characters should be prefixed with the length in the underlying stream.
            /// </remarks>
            /// <param name="buffer">The buffer that is allocated by the caller.</param>
            /// <param name="length">The length of the string.</param>
            /// <param name="encoding">The encoding of the characters.</param>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <returns>The string decoded from the log entry content stream.</returns>
            public async Task<string> ReadStringAsync(byte[] buffer, int length, Encoding encoding, CancellationToken token = default)
            {
                //TODO: Should be rewritten for .NET Standard 2.1
                var maxCharBytesSize = Math.Min(encoding.GetMaxByteCount(length), buffer.Length);
                var maxCharsSize = encoding.GetMaxCharCount(maxCharBytesSize);
                var sb = default(StringBuilder);
                using (var charBuffer = new ArrayRental<char>(maxCharsSize))
                {
                    int currentPos = 0;
                    do
                    {
                        var readLength = Math.Min(length - currentPos, maxCharBytesSize);
                        var n = await content.ReadAsync(buffer, 0, readLength, token).ConfigureAwait(false);
                        if (n == 0)
                            throw new EndOfStreamException();
                        var charsRead = encoding.GetChars(buffer, 0, n, charBuffer, 0);
                        if (currentPos == 0 && n == length)
                            return new string(charBuffer, 0, charsRead);
                        if (sb is null)
                            sb = new StringBuilder(length);
                        sb.Append(charBuffer, 0, charsRead);
                        currentPos += n;
                    }
                    while (currentPos < length);
                }
                return sb.ToString();
            }

            /// <summary>
            /// Copies the object content into the specified stream.
            /// </summary>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <param name="output">The output stream receiving object content.</param>
            public Task CopyToAsync(Stream output, CancellationToken token)
            {
                content.Adjust(contentOffset, Length);
                return content.CopyToAsync(output, 1024, token);
            }

            /// <summary>
            /// Copies the log entry content into the specified pipe writer.
            /// </summary>
            /// <param name="output">The writer.</param>
            /// <param name="token">The token that can be used to cancel operation.</param>
            /// <returns>The task representing asynchronous execution of this method.</returns>
            public ValueTask CopyToAsync(PipeWriter output, CancellationToken token)
            {
                content.Adjust(contentOffset, Length);
                return content.CopyToAsync(output, false, token: token);
            }

            long? IDataTransferObject.Length => Length;
            bool IDataTransferObject.IsReusable => true;

            /// <summary>
            /// Gets Raft term of this log entry.
            /// </summary>
            public long Term { get; }

            /// <summary>
            /// Gets timestamp of this log entry.
            /// </summary>
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

            private readonly long payloadOffset;
            private readonly BinaryReader reader;
            private readonly BinaryWriter writer;
            private readonly StreamSegment cachedContent;
            internal readonly long IndexOffset;

            internal Partition(string fileName, long recordsPerPartition)
                : base(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 2048, FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous)
            {
                payloadOffset = AllocationTableOffset + AllocationTableEntrySize * recordsPerPartition;
                Capacity = recordsPerPartition;
                if (Length == 0)
                    SetLength(payloadOffset);
                reader = new BinaryReader(this, Encoding.UTF8, true);
                writer = new BinaryWriter(this, Encoding.UTF8, true);
                cachedContent = new StreamSegment(this);
                //restore index offset
                Position = IndexOffsetOffset;
                IndexOffset = reader.ReadInt64();
            }

            internal Partition(DirectoryInfo location, long recordsPerPartition, long partitionNumber)
                : this(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), recordsPerPartition)
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
                    return new LogEntry(reader, cachedContent);
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
                    cachedContent.Dispose();
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

        private sealed class LogEntryList : List<IRaftLogEntry>, ILogEntryList<IRaftLogEntry>
        {
            private AsyncLock.Holder readLock;

            internal LogEntryList(int capacity, AsyncLock.Holder readLock) : base(capacity) => this.readLock = readLock;

            private void Dispose(bool disposing)
            {
                if (disposing)
                    Clear();
                readLock.Dispose();
            }

            void IDisposable.Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~LogEntryList() => Dispose(false);
        }
        

        private static readonly ValueFunc<long, long, long> MaxFunc = new ValueFunc<long, long, long>(Math.Max);
        private long lastIndex;
        private readonly long recordsPerPartition;
        //key is the number of partition
        private readonly Dictionary<long, Partition> partitionTable;
        private readonly NodeState state;
        private readonly DirectoryInfo location;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly ILogEntryList<IRaftLogEntry> emptyLog;
        private readonly ILogEntryList<IRaftLogEntry> initialLog;

        public PersistentState(DirectoryInfo location, long recordsPerPartition)
        {
            if(recordsPerPartition < 1L)
                throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
            if (!location.Exists)
                location.Create();
            this.location = location;
            this.recordsPerPartition = recordsPerPartition;
            commitEvent = new AsyncManualResetEvent(false);
            partitionTable = new Dictionary<long, Partition>();
            emptyLog = new LogEntryList<LogEntry>();
            initialLog = new LogEntryList<IRaftLogEntry>(InMemoryAuditTrail.EmptyLog);
            //load all partitions from file system
            foreach (var file in location.EnumerateFiles())
                if (long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.FullName, recordsPerPartition);
                    lastIndex += partition.Count;
                    partitionTable[partitionNumber] = partition;
                }
            //load node state
            state = new NodeState(Path.Combine(location.FullName, NodeState.FileName), AsyncLock.WriteLock(this));
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
                partition = new Partition(location, recordsPerPartition, partitionNumber);
                partitionTable.Add(partitionNumber, partition);
            }
            return partition;
        }

        private ILogEntryList<IRaftLogEntry> GetEntries(long startIndex, long endIndex, AsyncLock.Holder readLock)
        {
            if (startIndex > lastIndex.VolatileRead())
            {
                readLock.Dispose();
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            }
            ILogEntryList<IRaftLogEntry> result;
            if (partitionTable.Count > 0)
            {
                var list = new LogEntryList((int)Math.Min(int.MaxValue, endIndex - startIndex + 1L), readLock);
                for (var i = 0L; startIndex <= endIndex; startIndex++, i++)
                {
                    IRaftLogEntry entry;
                    if (TryGetPartition(startIndex, out var partition) && (entry = partition[startIndex]) != null)
                        list.Add(entry);
                }
                result = list;
            }
            else
            {
                result = startIndex == 0L ? initialLog : emptyLog;
                readLock.Dispose();
            }
            return result;
        }

        async Task<ILogEntryList<IRaftLogEntry>> IAuditTrail<IRaftLogEntry>.GetEntriesAsync(long startIndex, long? endIndex)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return emptyLog;
            var readLock = await this.AcquireReadLockAsync(CancellationToken.None).ConfigureAwait(false);
            return GetEntries(startIndex, endIndex ?? lastIndex, readLock);
        }

        private async Task<long> AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex)
        {
            if (startIndex <= state.CommitIndex)
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
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

        async Task<long> IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long? startIndex)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await this.AcquireWriteLockAsync(CancellationToken.None).ConfigureAwait(false))
                return await AppendAsync(entries, startIndex ?? lastIndex + 1L).ConfigureAwait(false);
        }

        Task IAuditTrail.WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => index >= 0L ? CommitEvent.WaitForCommitAsync(this, commitEvent, index, timeout, token) : Task.FromException(new ArgumentOutOfRangeException(nameof(index)));

        Task<long> IAuditTrail.CommitAsync(long? endIndex)
        {
            throw new NotImplementedException();
        }

        ref readonly IRaftLogEntry IAuditTrail<IRaftLogEntry>.First => ref InMemoryAuditTrail.EmptyLog[0];

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
                foreach (var partition in partitionTable.Values)
                    partition.Dispose();
                partitionTable.Clear();
                state.Dispose();
                commitEvent.Dispose();
                emptyLog.Dispose();
                initialLog.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}