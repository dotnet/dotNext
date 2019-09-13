using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using Replication;
    using Text;
    using Threading;
    using static Collections.Generic.Dictionary;

    /// <summary>
    /// Represents general purpose persistent audit trail compatible with Raft algorithm.
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
    /// The audit trail supports log compaction. However, it doesn't know how to interpret and reduce log records during compaction.
    /// To do that, you can override <see cref="CreateSnapshotBuilder"/> method and implement state machine logic.
    /// </remarks>
    public class PersistentState : Disposable, IPersistentState
    {
        internal readonly struct LogEntryMetadata
        {
            internal readonly long Term, Timestamp, Length, Offset;

            internal LogEntryMetadata(IRaftLogEntry entry, long offset, long length)
            {
                Term = entry.Term;
                Timestamp = entry.Timestamp.UtcTicks;
                Length = length;
                Offset = offset;
            }

            internal static unsafe int Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => sizeof(LogEntryMetadata);
            }
        }

         /// <summary>
         /// Represents persistent log entry.
         /// </summary>
        protected sealed class LogEntry : IRaftLogEntry
        {
            private readonly StreamSegment content;
            private readonly LogEntryMetadata metadata;
            private readonly byte[] buffer;

            internal LogEntry(StreamSegment cachedContent, byte[] sharedBuffer, in LogEntryMetadata metadata, bool snapshot = false)
            {
                this.metadata = metadata;
                content = cachedContent;
                buffer = sharedBuffer;
                IsSnapshot = snapshot;
            }

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length => metadata.Length;

            /// <summary>
            /// Gets a value indicating that this entry is a snapshot entry.
            /// </summary>
            public bool IsSnapshot { get; }

            internal LogEntry AdjustPosition()
            {
                content.Adjust(metadata.Offset, Length);
                return this;
            }

            /// <summary>
            /// Reads the number of bytes using the pre-allocated buffer.
            /// </summary>
            /// <remarks>
            /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
            /// </remarks>
            /// <param name="count">The number of bytes to read.</param>
            /// <returns>The span of bytes representing buffer segment.</returns>
            /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
            public ReadOnlySpan<byte> Read(int count)
                => content.ReadBytes(count, buffer);

            /// <summary>
            /// Reads asynchronously the number of bytes using the pre-allocated buffer.
            /// </summary>
            /// <remarks>
            /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
            /// </remarks>
            /// <param name="count">The number of bytes to read.</param>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <returns>The span of bytes representing buffer segment.</returns>
            /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
            public Task<ReadOnlyMemory<byte>> ReadAsync(int count, CancellationToken token = default)
                => content.ReadBytesAsync(count, buffer, token);

            /// <summary>
            /// Reads the string using the specified encoding.
            /// </summary>
            /// <remarks>
            /// The characters should be prefixed with the length in the underlying stream.
            /// </remarks>
            /// <param name="length">The length of the string, in bytes.</param>
            /// <param name="context">The decoding context.</param>
            /// <returns>The string decoded from the log entry content stream.</returns>
            public string ReadString(int length, DecodingContext context)
                => content.ReadString(length, context, buffer);

            /// <summary>
            /// Reads the string asynchronously using the specified encoding.
            /// </summary>
            /// <remarks>
            /// The characters should be prefixed with the length in the underlying stream.
            /// </remarks>
            /// <param name="length">The length of the string, in bytes.</param>
            /// <param name="context">The decoding context.</param>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <returns>The string decoded from the log entry content stream.</returns>
            public Task<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
                => content.ReadStringAsync(length, context, buffer, token);

            /// <summary>
            /// Copies the object content into the specified stream.
            /// </summary>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <param name="output">The output stream receiving object content.</param>
            public Task CopyToAsync(Stream output, CancellationToken token) => AdjustPosition().content.CopyToAsync(output, buffer, token);

            /// <summary>
            /// Copies the log entry content into the specified pipe writer.
            /// </summary>
            /// <param name="output">The writer.</param>
            /// <param name="token">The token that can be used to cancel operation.</param>
            /// <returns>The task representing asynchronous execution of this method.</returns>
            public ValueTask CopyToAsync(PipeWriter output, CancellationToken token) => AdjustPosition().content.CopyToAsync(output, false, buffer, token);

            long? IDataTransferObject.Length => Length;
            bool IDataTransferObject.IsReusable => true;

            /// <summary>
            /// Gets Raft term of this log entry.
            /// </summary>
            public long Term => metadata.Term;

            /// <summary>
            /// Gets timestamp of this log entry.
            /// </summary>
            public DateTimeOffset Timestamp => new DateTimeOffset(metadata.Timestamp, TimeSpan.Zero);
        }

        /*
            Partition file format:
            FileName - number of partition
            Allocation table:
            [struct LogEntryMetadata] X number of entries
            Payload:
            [octet string] X number of entries
         */
        private sealed class Partition : FileStream
        {
            private readonly long payloadOffset;
            internal readonly long IndexOffset;
            internal readonly long Capacity;    //max number of entries
            private readonly byte[] buffer;
            private readonly StreamSegment segment;
            private readonly LogEntryMetadata[] lookupCache;

            internal Partition(DirectoryInfo location, byte[] sharedBuffer, long recordsPerPartition, long partitionNumber, bool useLookupCache)
                : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, sharedBuffer.Length, FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous)
            {
                payloadOffset = LogEntryMetadata.Size * recordsPerPartition;
                Capacity = recordsPerPartition;
                buffer = sharedBuffer;
                if (Length == 0)
                    SetLength(payloadOffset);
                segment = new StreamSegment(this);
                IndexOffset = partitionNumber * recordsPerPartition;
                lookupCache = useLookupCache ? new LogEntryMetadata[recordsPerPartition] : null;
            }

            internal async Task<LogEntry> ReadAsync(long index, bool absoluteIndex, CancellationToken token)
            {
                //calculate relative index
                if (absoluteIndex)
                    index -= IndexOffset;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {IndexOffset}");
                //find pointer to the content
                LogEntryMetadata metadata;
                if (lookupCache is null)
                {
                    Position = index * LogEntryMetadata.Size;
                    metadata = MemoryMarshal.Read<LogEntryMetadata>((await this.ReadBytesAsync(LogEntryMetadata.Size, buffer, token).ConfigureAwait(false)).Span);
                }
                else
                    metadata = lookupCache[index];
                if (metadata.Offset == 0L)
                    return null;
                return new LogEntry(segment, buffer, metadata);
            }

            internal async Task WriteAsync(IRaftLogEntry entry, long index)
            {
                //calculate relative index
                index -= IndexOffset;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {IndexOffset}");
                //calculate offset of the previous entry
                long offset;
                LogEntryMetadata metadata;
                if (index == 0L || index == 1L && IndexOffset == 0L)
                    offset = payloadOffset;
                else if (lookupCache is null)
                {
                    //read content offset and the length of the previous entry
                    Position = (index - 1) * LogEntryMetadata.Size;
                    metadata = MemoryMarshal.Read<LogEntryMetadata>((await this.ReadBytesAsync(LogEntryMetadata.Size, buffer).ConfigureAwait(false)).Span);
                    Debug.Assert(metadata.Offset > 0, "Previous entry doesn't exist for unknown reason");
                    offset = metadata.Length + metadata.Offset;
                }
                else
                {
                    metadata = lookupCache[index - 1];
                    Debug.Assert(metadata.Offset > 0, "Previous entry doesn't exist for unknown reason");
                    offset = metadata.Length + metadata.Offset;
                }
                //write content
                Position = offset;
                await entry.CopyToAsync(this).ConfigureAwait(false);
                metadata = new LogEntryMetadata(entry, offset, Position - offset);
                //record new log entry to the allocation table
                MemoryMarshal.Write(buffer, ref metadata);
                Position = index * LogEntryMetadata.Size;
                await WriteAsync(buffer, 0, LogEntryMetadata.Size).ConfigureAwait(false);
                //update cache
                if (!(lookupCache is null))
                    lookupCache[index] = metadata;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    segment.Dispose();
                base.Dispose(disposing);
            }
        }

        /*
         * Binary format:
         * [struct LogEntryMetadata] X 1
         * [octet string] X 1
         */
        private sealed class Snapshot : FileStream
        {
            private const string FileName = "snapshot";
            private readonly byte[] buffer;
            private readonly StreamSegment segment;

            internal Snapshot(DirectoryInfo location, byte[] sharedBuffer)
                : base(Path.Combine(location.FullName, FileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, sharedBuffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough)
            {
                buffer = sharedBuffer;
                segment = new StreamSegment(this);
            }

            internal async Task SaveAsync(IRaftLogEntry entry, CancellationToken token)
            {
                Position = LogEntryMetadata.Size;
                await entry.CopyToAsync(this, token).ConfigureAwait(false);
                var metadata = new LogEntryMetadata(entry, LogEntryMetadata.Size, Length - LogEntryMetadata.Size);
                Position = 0;
                MemoryMarshal.Write(buffer, ref metadata);
                await WriteAsync(buffer, 0, LogEntryMetadata.Size, token).ConfigureAwait(false);
            }

            internal async Task<LogEntry> LoadAsync(CancellationToken token)
            {
                Position = 0;
                var metadata = MemoryMarshal.Read<LogEntryMetadata>((await this.ReadBytesAsync(LogEntryMetadata.Size, buffer, token).ConfigureAwait(false)).Span);
                return new LogEntry(segment, buffer, metadata, true);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    segment.Dispose();
                base.Dispose(disposing);
            }
        }

        /*
            State file format:
            8 bytes = Term
            8 bytes = CommitIndex
            8 bytes = LastApplied
            8 bytes = LastIndex
            4 bytes = Node port
            4 bytes = Address Length
            octet string = IP Address
         */
        private sealed class NodeState : Disposable
        {
            private const string FileName = "node.state";
            private const long Capacity = 1024; //1 KB
            private const long TermOffset = 0L;
            private const long CommitIndexOffset = TermOffset + sizeof(long);
            private const long LastAppliedOffset = CommitIndexOffset + sizeof(long);
            private const long LastIndexOffset = LastAppliedOffset + sizeof(long);
            private const long PortOffset = LastIndexOffset + sizeof(long);
            private const long AddressLengthOffset = PortOffset + sizeof(int);
            private const long AddressOffset = AddressLengthOffset + sizeof(int);
            private readonly MemoryMappedFile mappedFile;
            private readonly MemoryMappedViewAccessor stateView;
            private readonly AsyncLock syncRoot;
            private volatile IPEndPoint votedFor;
            private long term, commitIndex, lastIndex, lastApplied;  //volatile

            internal NodeState(DirectoryInfo location, AsyncLock writeLock)
            {
                mappedFile = MemoryMappedFile.CreateFromFile(Path.Combine(location.FullName, FileName), FileMode.OpenOrCreate, null, Capacity, MemoryMappedFileAccess.ReadWrite);
                syncRoot = writeLock;
                stateView = mappedFile.CreateViewAccessor();
                term = stateView.ReadInt64(TermOffset);
                commitIndex = stateView.ReadInt64(CommitIndexOffset);
                lastIndex = stateView.ReadInt64(LastIndexOffset);
                lastApplied = stateView.ReadInt64(LastAppliedOffset);
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

            internal void Flush() => stateView.Flush();

            internal long CommitIndex
            {
                get => commitIndex.VolatileRead();
                set
                {
                    stateView.Write(CommitIndexOffset, value);
                    commitIndex.VolatileWrite(value);
                }
            }

            internal long LastApplied
            {
                get => lastApplied.VolatileRead();
                set
                {
                    stateView.Write(LastAppliedOffset, value);
                    lastApplied.VolatileWrite(value);
                }
            }

            internal long LastIndex
            {
                get => lastIndex.VolatileRead();
                set
                {
                    stateView.Write(LastIndexOffset, value);
                    lastIndex.VolatileWrite(value);
                }
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

        /// <summary>
        /// Represents snapshot builder.
        /// </summary>
        protected abstract class SnapshotBuilder: Disposable, IRaftLogEntry
        {
            private readonly DateTimeOffset timestamp;
            private long term;

            /// <summary>
            /// Initializes a new snapshot builder.
            /// </summary>
            protected SnapshotBuilder()
            {
                timestamp = DateTimeOffset.UtcNow;
                term = InMemoryAuditTrail.InitialLog[0].Term;
            }

            /// <summary>
            /// Interprets the command specified by the log entry.
            /// </summary>
            /// <param name="entry">The committed log entry.</param>
            /// <returns>The task representing asynchronous execution of this method.</returns>
            protected abstract ValueTask ApplyAsync(LogEntry entry);

            internal ValueTask ApplyCoreAsync(LogEntry entry)
            {
                term = Math.Max(entry.Term, term);
                return ApplyAsync(entry);
            }

            long? IDataTransferObject.Length => null;

            long IRaftLogEntry.Term => term;

            DateTimeOffset ILogEntry.Timestamp => timestamp;

            bool ILogEntry.IsSnapshot => true;

            bool IDataTransferObject.IsReusable => false;

            /// <summary>
            /// Copies the reduced command into the specified stream.
            /// </summary>
            /// <param name="output">The write-only stream.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing asynchronous state of this operation.</returns>
            public abstract Task CopyToAsync(Stream output, CancellationToken token);

            /// <summary>
            /// Copies the reduced command into the specified pipe.
            /// </summary>
            /// <param name="output">The write-only representation of the pipe.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing asynchronous state of this operation.</returns>
            public abstract ValueTask CopyToAsync(PipeWriter output, CancellationToken token);
        }

        private const int DefaultBufferSize = 2048;
        private const int MinBufferSize = 128;
        private readonly long recordsPerPartition;
        //key is the number of partition
        private readonly Dictionary<long, Partition> partitionTable;
        private readonly NodeState state;
        private readonly Snapshot snapshot;
        private readonly DirectoryInfo location;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly AsyncExclusiveLock syncRoot;
        private readonly ILogEntryList<IRaftLogEntry> emptyLog;
        private readonly ILogEntryList<IRaftLogEntry> initialLog;
        private readonly byte[] sharedBuffer;
        private readonly bool useLookupCache;

        /// <summary>
        /// Initializes a new persistent audit trail.
        /// </summary>
        /// <param name="path">The path to the folder to be used by audit trail.</param>
        /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
        /// <param name="bufferSize">Optional size of in-memory buffer for I/O operations.</param>
        /// <param name="useCaching"><see langword="true"/> to in-memory cache for faster read/write of log entries; <see langword="false"/> to reduce the memory by the cost of the performance.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 1; or <paramref name="bufferSize"/> is too small.</exception>
        public PersistentState(DirectoryInfo path, long recordsPerPartition, int bufferSize = DefaultBufferSize, bool useCaching = true)
        {
            if (bufferSize < MinBufferSize)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (recordsPerPartition < 1L)
                throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
            if (!path.Exists)
                path.Create();
            sharedBuffer = new byte[bufferSize];
            location = path;
            useLookupCache = useCaching;
            this.recordsPerPartition = recordsPerPartition;
            commitEvent = new AsyncManualResetEvent(false);
            syncRoot = new AsyncExclusiveLock();
            partitionTable = new Dictionary<long, Partition>();
            emptyLog = new LogEntryList<LogEntry>();
            initialLog = new LogEntryList<IRaftLogEntry>(InMemoryAuditTrail.InitialLog);
            //load all partitions from file system
            foreach (var file in path.EnumerateFiles())
                if (long.TryParse(file.Name, out var partitionNumber))
                    partitionTable[partitionNumber] = new Partition(file.Directory, sharedBuffer, recordsPerPartition, partitionNumber, useCaching);
            state = new NodeState(path, AsyncLock.Exclusive(syncRoot));
            snapshot = new Snapshot(path, sharedBuffer);
        }

        /// <summary>
        /// Initializes a new persistent audit trail.
        /// </summary>
        /// <param name="path">The path to the folder to be used by audit trail.</param>
        /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
        /// <param name="bufferSize">Optional size of in-memory buffer for I/O operations.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 1.</exception>
        public PersistentState(string path, long recordsPerPartition, int bufferSize = DefaultBufferSize)
            : this(new DirectoryInfo(path), recordsPerPartition, bufferSize)
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
        public long GetLastIndex(bool committed) => committed ? state.CommitIndex : state.LastIndex;

        private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

        private bool TryGetPartition(long recordIndex, out Partition partition)
            => partitionTable.TryGetValue(PartitionOf(recordIndex), out partition);

        private Partition GetOrCreatePartition(long recordIndex, out long partitionNumber)
        {
            partitionNumber = PartitionOf(recordIndex);
            if (!partitionTable.TryGetValue(partitionNumber, out var partition))
            {
                partition = new Partition(location, sharedBuffer, recordsPerPartition, partitionNumber, useLookupCache);
                partitionTable.Add(partitionNumber, partition);
            }
            return partition;
        }

        private async Task<ILogEntryList<IRaftLogEntry>> GetEntries(long startIndex, long endIndex, AsyncLock.Holder readLock, CancellationToken token)
        {
            if (startIndex > state.LastIndex)
            {
                readLock.Dispose();
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            }
            if(endIndex > state.LastIndex)
            {
                readLock.Dispose();
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            }
            ILogEntryList<IRaftLogEntry> result;
            if (partitionTable.Count > 0)
                try
                {
                    var list = new LogEntryList((int)Math.Min(int.MaxValue, endIndex - startIndex + 1L), readLock);
                    for (; startIndex <= endIndex; startIndex++)
                    {
                        IRaftLogEntry entry;
                        if (startIndex == 0L)   //handle ephemeral entity
                            entry = InMemoryAuditTrail.InitialLog[0];
                        else if (TryGetPartition(startIndex, out var partition)) //handle regular record
                            entry = await partition.ReadAsync(startIndex, true, token).ConfigureAwait(false);
                        else if (snapshot.Length > 0 && startIndex <= state.CommitIndex)    //probably the record is snapshotted
                        {
                            entry = await snapshot.LoadAsync(token).ConfigureAwait(false);
                            //skip squashed log entries
                            startIndex = state.CommitIndex - (state.CommitIndex + 1) % recordsPerPartition;
                        }
                        else
                            break;
                        Debug.Assert(entry != null);
                        list.Add(entry);
                    }
                    result = list;
                }
                catch
                {
                    readLock.Dispose();
                    throw;
                }
            else
            {
                result = startIndex == 0L ? initialLog : emptyLog;
                readLock.Dispose();
            }
            return result;
        }

        async Task<ILogEntryList<IRaftLogEntry>> IAuditTrail<IRaftLogEntry>.GetEntriesAsync(long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return emptyLog;
            var readLock = await syncRoot.AcquireLockAsync(token).ConfigureAwait(false);
            return await GetEntries(startIndex, endIndex, readLock, token).ConfigureAwait(false);
        }

        async Task<ILogEntryList<IRaftLogEntry>> IAuditTrail<IRaftLogEntry>.GetEntriesAsync(long startIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            var readLock = await syncRoot.AcquireLockAsync(token).ConfigureAwait(false);
            return await GetEntries(startIndex, state.LastIndex, readLock, token).ConfigureAwait(false);
        }

        private async Task<long> AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex)
        {
            if (startIndex <= state.CommitIndex)
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            if (startIndex > state.LastIndex + 1)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            long partitionLow = 0, partitionHigh = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var insertionIndex = startIndex + i;
                await GetOrCreatePartition(insertionIndex, out var partitionNumber).WriteAsync(entries[i], insertionIndex).ConfigureAwait(false);
                state.LastIndex = insertionIndex;
                partitionLow = Math.Min(partitionLow, partitionNumber);
                partitionHigh = Math.Max(partitionHigh, partitionNumber);
            }
            //flush all touched partitions
            state.Flush();
            Task flushTask;
            switch (partitionHigh - partitionLow)
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

        async Task IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(entries, startIndex).ConfigureAwait(false);
        }

        async Task<long> IAuditTrail<IRaftLogEntry>.AppendAsync(IReadOnlyList<IRaftLogEntry> entries)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
                return await AppendAsync(entries, state.LastIndex + 1L).ConfigureAwait(false);
        }

        Task IAuditTrail.WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => index >= 0L ? CommitEvent.WaitForCommitAsync(this, commitEvent, index, timeout, token) : Task.FromException(new ArgumentOutOfRangeException(nameof(index)));

        /// <summary>
        /// Creates a new snapshot builder.
        /// </summary>
        /// <param name="buffer">Preallocated buffer that can be used to perform I/O operations.</param>
        /// <returns>The snapshot builder; or <see langword="null"/> if snapshotting is not supported.</returns>
        protected virtual SnapshotBuilder CreateSnapshotBuilder(byte[] buffer) => null;

        private async Task ForceCompaction(SnapshotBuilder builder, long commitIndex, CancellationToken token)
        {
            //1. Find the partitions that can be compacted
            var compactionScope = new SortedDictionary<long, Partition>();
            foreach (var (partNumber, partition) in partitionTable)
            {
                token.ThrowIfCancellationRequested();
                if (partition.IndexOffset + partition.Capacity <= commitIndex)
                    compactionScope.Add(partNumber, partition);
            }
            //2. Do compaction
            foreach (var partition in compactionScope.Values)
                for (var i = 0L; i < partition.Capacity; i++)
                    if (partition.IndexOffset > 0L || i > 0L) //ignore the ephemeral entry
                        await builder.ApplyCoreAsync((await partition.ReadAsync(i, false, token).ConfigureAwait(false)).AdjustPosition()).ConfigureAwait(false);
            //3. Persist snapshot
            await snapshot.SaveAsync(builder, token).ConfigureAwait(false);
            //4. Remove snapshotted partitions
            foreach (var (partNumber, partition) in compactionScope)
            {
                partitionTable.Remove(partNumber);
                var fileName = partition.Name;
                partition.Dispose();
                File.Delete(fileName);
            }
            compactionScope.Clear();
        }

        private Task ForceCompaction(CancellationToken token)
        {
            var builder = CreateSnapshotBuilder(sharedBuffer);
            if (builder is null)
                return Task.CompletedTask;
            else
            {
                var commitIndex = state.CommitIndex;
                try
                {
                    return commitIndex >= recordsPerPartition ? ForceCompaction(builder, commitIndex, token) : Task.CompletedTask;
                }
                finally
                {
                    builder.Dispose();
                }
            }
        }

        private async Task<long> CommitAsync(long? endIndex, CancellationToken token)
        {
            long count;
            using (await syncRoot.AcquireLockAsync(token).ConfigureAwait(false))
            {
                var startIndex = state.CommitIndex + 1L;
                count = (endIndex ?? GetLastIndex(false)) - startIndex + 1L;
                if (count > 0)
                {
                    state.CommitIndex = startIndex + count - 1;
                    await ApplyAsync(token).ConfigureAwait(false);
                    await ForceCompaction(token).ConfigureAwait(false);
                }
            }
            return Math.Max(count, 0L);
        }

        Task<long> IAuditTrail.CommitAsync(long endIndex, CancellationToken token) => CommitAsync(endIndex, token);

        Task<long> IAuditTrail.CommitAsync(CancellationToken token) => CommitAsync(null, token);

        /// <summary>
        /// Applies the command represented by the log entry to the underlying database engine.
        /// </summary>
        /// <remarks>
        /// The base method does nothing so you don't need to call base implementation.
        /// </remarks>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask ApplyAsync(LogEntry entry) => new ValueTask(Task.CompletedTask);

        private async Task ApplyAsync(CancellationToken token)
        {
            for (var i = state.LastApplied + 1L; i <= state.CommitIndex; state.LastApplied = i++)
                if (TryGetPartition(i, out var partition))
                    await ApplyAsync((await partition.ReadAsync(i, true, token).ConfigureAwait(false)).AdjustPosition()).ConfigureAwait(false);
                else
                    Debug.Fail($"Log entry with index {i} doesn't have partition");
            state.Flush();
        }

        async Task IAuditTrail.EnsureConsistencyAsync(CancellationToken token)
        {
            using (await syncRoot.AcquireLockAsync(token).ConfigureAwait(false))
                await ApplyAsync(token).ConfigureAwait(false);
        }

        ref readonly IRaftLogEntry IAuditTrail<IRaftLogEntry>.First => ref InMemoryAuditTrail.InitialLog[0];

        bool IPersistentState.IsVotedFor(IRaftClusterMember member) => state.IsVotedFor(member?.Endpoint);

        long IPersistentState.Term => state.Term;

        ValueTask<long> IPersistentState.IncrementTermAsync() => state.IncrementTermAsync();

        ValueTask IPersistentState.UpdateTermAsync(long term) => state.UpdateTermAsync(term);

        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember member) => state.UpdateVotedForAsync(member?.Endpoint);

        /// <summary>
        /// Releases all resources associated with this audit trail.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="IDisposable.Dispose()"/>; <see langword="false"/> if called from finalizer.</param>
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
                syncRoot.Dispose();
                snapshot.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}