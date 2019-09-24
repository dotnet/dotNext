using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    using Buffers;
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

        internal readonly struct SnapshotMetadata
        {
            internal readonly long Index;
            internal readonly LogEntryMetadata RecordMetadata;

            internal SnapshotMetadata(IRaftLogEntry snapshot, long index, long length)
            {
                Index = index;
                RecordMetadata = new LogEntryMetadata(snapshot, Size, length);
            }

            internal static unsafe int Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => sizeof(SnapshotMetadata);
            }
        }

        /// <summary>
        /// Represents persistent log entry.
        /// </summary>
        protected readonly struct LogEntry : IRaftLogEntry
        {
            private readonly StreamSegment content;
            private readonly LogEntryMetadata metadata;
            private readonly byte[] buffer;
            internal readonly long? SnapshotIndex;

            internal LogEntry(StreamSegment cachedContent, byte[] sharedBuffer, in LogEntryMetadata metadata)
            {
                this.metadata = metadata;
                content = cachedContent;
                buffer = sharedBuffer;
                SnapshotIndex = null;
            }

            internal LogEntry(StreamSegment cachedContent, byte[] sharedBuffer, in SnapshotMetadata metadata)
            {
                this.metadata = metadata.RecordMetadata;
                content = cachedContent;
                buffer = sharedBuffer;
                SnapshotIndex = metadata.Index;
            }

            bool ILogEntry.IsSnapshot => SnapshotIndex.HasValue;

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length => metadata.Length;

            internal Stream AdjustPosition()
            {
                content.Adjust(metadata.Offset, Length);
                return content;
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
            public Task CopyToAsync(Stream output, CancellationToken token) => AdjustPosition().CopyToAsync(output, buffer, token);

            /// <summary>
            /// Copies the object content into the specified stream synchronously.
            /// </summary>
            /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
            /// <param name="output">The output stream receiving object content.</param>
            public void CopyTo(Stream output, CancellationToken token) => AdjustPosition().CopyTo(output, buffer, token);

            /// <summary>
            /// Copies the log entry content into the specified pipe writer.
            /// </summary>
            /// <param name="output">The writer.</param>
            /// <param name="token">The token that can be used to cancel operation.</param>
            /// <returns>The task representing asynchronous execution of this method.</returns>
            public ValueTask CopyToAsync(PipeWriter output, CancellationToken token) => AdjustPosition().CopyToAsync(output, false, buffer, token);

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
            internal readonly long FirstIndex;
            internal readonly long Capacity;    //max number of entries
            private readonly byte[] buffer;
            private readonly StreamSegment segment;
            private readonly LogEntryMetadata[] lookupCache;

            internal Partition(DirectoryInfo location, byte[] sharedBuffer, long recordsPerPartition, long partitionNumber, bool useLookupCache)
                : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, sharedBuffer.Length, FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous)
            {
                Capacity = recordsPerPartition;
                buffer = sharedBuffer;
                segment = new StreamSegment(this);
                FirstIndex = partitionNumber * recordsPerPartition;
                lookupCache = useLookupCache ? new LogEntryMetadata[recordsPerPartition] : null;
            }

            private long PayloadOffset => LogEntryMetadata.Size * Capacity;

            internal long LastIndex => FirstIndex + Capacity - 1;

            internal void Allocate(long initialSize) => SetLength(initialSize + PayloadOffset);

            internal void PopulateCache()
            {
                if (lookupCache != null)
                    for (int index = 0, count; index < lookupCache.Length; index += count)
                    {
                        count = Math.Min(buffer.Length / LogEntryMetadata.Size, lookupCache.Length - index);
                        var maxBytes = count * LogEntryMetadata.Size;
                        if (Read(buffer, 0, maxBytes) < maxBytes)
                            throw new EndOfStreamException();
                        var source = new Span<byte>(buffer, 0, maxBytes);
                        var destination = MemoryMarshal.AsBytes(new Span<LogEntryMetadata>(lookupCache).Slice(index));
                        source.CopyTo(destination);
                    }
            }

            internal async ValueTask<LogEntry?> ReadAsync(long index, bool absoluteIndex, CancellationToken token)
            {
                //calculate relative index
                if (absoluteIndex)
                    index -= FirstIndex;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");
                //find pointer to the content
                LogEntryMetadata metadata;
                if (lookupCache is null)
                {
                    Position = index * LogEntryMetadata.Size;
                    metadata = await this.ReadAsync<LogEntryMetadata>(buffer, token).ConfigureAwait(false);
                }
                else
                    metadata = lookupCache[index];
                return metadata.Offset > 0 ? new LogEntry(segment, buffer, metadata) : new LogEntry?();
            }

            internal async ValueTask WriteAsync(IRaftLogEntry entry, long index)
            {
                //calculate relative index
                index -= FirstIndex;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");
                //calculate offset of the previous entry
                long offset;
                LogEntryMetadata metadata;
                if (index == 0L || index == 1L && FirstIndex == 0L)
                    offset = PayloadOffset;
                else if (lookupCache is null)
                {
                    //read content offset and the length of the previous entry
                    Position = (index - 1) * LogEntryMetadata.Size;
                    metadata = await this.ReadAsync<LogEntryMetadata>(buffer).ConfigureAwait(false);
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
                Position = index * LogEntryMetadata.Size;
                await this.WriteAsync(ref metadata, buffer).ConfigureAwait(false);
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
         * [struct SnapshotMetadata] X 1
         * [octet string] X 1
         */
        private sealed class Snapshot : FileStream
        {
            private const string FileName = "snapshot";
            private const string TempFileName = "snapshot.new";
            private readonly byte[] buffer;
            private readonly StreamSegment segment;

            internal Snapshot(DirectoryInfo location, byte[] sharedBuffer, bool tempSnapshot = false)
                : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, sharedBuffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess | FileOptions.WriteThrough)
            {
                buffer = sharedBuffer;
                segment = new StreamSegment(this);
            }

            internal void PopulateCache() => Index = Length > 0L ? this.Read<SnapshotMetadata>(buffer).Index : 0L;

            internal async ValueTask WriteAsync(IRaftLogEntry entry, long index, CancellationToken token)
            {
                Index = index;
                Position = SnapshotMetadata.Size;
                await entry.CopyToAsync(this, token).ConfigureAwait(false);
                var metadata = new SnapshotMetadata(entry, index, Length - SnapshotMetadata.Size);
                Position = 0;
                await this.WriteAsync(ref metadata, buffer, token).ConfigureAwait(false);
            }

            internal async ValueTask<LogEntry> ReadAsync(CancellationToken token)
            {
                Position = 0;
                return new LogEntry(segment, buffer, await this.ReadAsync<SnapshotMetadata>(buffer, token).ConfigureAwait(false));
            }

            //cached index of the snapshotted entry
            internal long Index
            {
                get;
                private set;
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
            octet string = IP Address (16 bytes for IPv6)
         */
        private sealed class NodeState : Disposable
        {
            private const string FileName = "node.state";
            private const long Capacity = 128;
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

        private readonly struct SingletonEntryList : IReadOnlyList<LogEntry>
        {
            private readonly LogEntry entry;

            internal SingletonEntryList(LogEntry entry)
            {
                this.entry = entry;
            }

            int IReadOnlyCollection<LogEntry>.Count => 1;

            LogEntry IReadOnlyList<LogEntry>.this[int index] => index == 0 ? entry : throw new IndexOutOfRangeException();

            public IEnumerator<LogEntry> GetEnumerator()
            {
                yield return entry;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Represents snapshot builder.
        /// </summary>
        protected abstract class SnapshotBuilder : Disposable, IRaftLogEntry
        {
            private readonly DateTimeOffset timestamp;
            private long term;

            /// <summary>
            /// Initializes a new snapshot builder.
            /// </summary>
            protected SnapshotBuilder() => timestamp = DateTimeOffset.UtcNow;

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

            bool IDataTransferObject.IsReusable => false;

            bool ILogEntry.IsSnapshot => true;

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
        private const long DefaultPartitionSize = 0;
        private readonly long recordsPerPartition;
        //key is the number of partition
        private readonly IDictionary<long, Partition> partitionTable;
        private readonly NodeState state;
        private Snapshot snapshot;
        private readonly DirectoryInfo location;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly AsyncExclusiveLock syncRoot;
        private readonly IRaftLogEntry initialEntry;
        /// <summary>
        /// Represents shared buffer that can be used for I/O operations.
        /// </summary>
        [SuppressMessage("Design", "CA1051", Justification = "It is protected field")]
        protected readonly byte[] sharedBuffer;
        private readonly bool useLookupCache;
        private readonly long initialSize;
        private readonly ArrayPool<LogEntry> entryPool;
        private readonly StreamSegment nullSegment;

        /// <summary>
        /// Initializes a new persistent audit trail.
        /// </summary>
        /// <param name="path">The path to the folder to be used by audit trail.</param>
        /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
        /// <param name="bufferSize">Optional size of in-memory buffer for I/O operations.</param>
        /// <param name="initialPartitionSize">The initial size of the file that holds the partition with log entries.</param>
        /// <param name="useCaching"><see langword="true"/> to in-memory cache for faster read/write of log entries; <see langword="false"/> to reduce the memory by the cost of the performance.</param>
        /// <param name="useSharedPool"><see langword="true"/> to use <see cref="ArrayPool{T}.Shared"/> pool for internal purposes; <see langword="false"/> to use dedicated pool of arrays.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 1; or <paramref name="bufferSize"/> is too small.</exception>
        public PersistentState(DirectoryInfo path, long recordsPerPartition, int bufferSize = DefaultBufferSize, long initialPartitionSize = DefaultPartitionSize, bool useCaching = true, bool useSharedPool = true)
        {
            if (bufferSize < MinBufferSize)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (recordsPerPartition < 2L)
                throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
            if (!path.Exists)
                path.Create();
            sharedBuffer = new byte[bufferSize];
            location = path;
            useLookupCache = useCaching;
            this.recordsPerPartition = recordsPerPartition;
            initialSize = initialPartitionSize;
            commitEvent = new AsyncManualResetEvent(false);
            syncRoot = new AsyncExclusiveLock();
            entryPool = useSharedPool ? ArrayPool<LogEntry>.Shared : ArrayPool<LogEntry>.Create();
            nullSegment = new StreamSegment(Stream.Null);
            initialEntry = new LogEntry(nullSegment, sharedBuffer, new LogEntryMetadata());
            //sorted dictionary to improve performance of log compaction and snapshot installation procedures
            partitionTable = new SortedDictionary<long, Partition>();
            //load all partitions from file system
            foreach (var file in path.EnumerateFiles())
                if (long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.Directory, sharedBuffer, recordsPerPartition, partitionNumber, useCaching);
                    partition.PopulateCache();
                    partitionTable[partitionNumber] = partition;
                }
            state = new NodeState(path, AsyncLock.Exclusive(syncRoot));
            snapshot = new Snapshot(path, sharedBuffer);
            snapshot.PopulateCache();
        }

        /// <summary>
        /// Initializes a new persistent audit trail.
        /// </summary>
        /// <param name="path">The path to the folder to be used by audit trail.</param>
        /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
        /// <param name="bufferSize">Optional size of in-memory buffer for I/O operations.</param>
        /// <param name="initialPartitionSize">The initial size of the file that holds the partition with log entries.</param>
        /// <param name="useCaching"><see langword="true"/> to in-memory cache for faster read/write of log entries; <see langword="false"/> to reduce the memory by the cost of the performance.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 1.</exception>
        public PersistentState(string path, long recordsPerPartition, int bufferSize = DefaultBufferSize, long initialPartitionSize = DefaultPartitionSize, bool useCaching = true)
            : this(new DirectoryInfo(path), recordsPerPartition, bufferSize, initialPartitionSize, useCaching)
        {
        }

        /// <summary>
        /// Gets the lock that can be used to synchronize access to this object.
        /// </summary>
        protected AsyncLock SyncRoot => AsyncLock.Exclusive(syncRoot);

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

        private Partition GetOrCreatePartition(long recordIndex)
        {
            var partitionNumber = PartitionOf(recordIndex);
            if (!partitionTable.TryGetValue(partitionNumber, out var partition))
            {
                partition = new Partition(location, sharedBuffer, recordsPerPartition, partitionNumber, useLookupCache);
                partition.Allocate(initialSize);
                partitionTable.Add(partitionNumber, partition);
            }
            return partition;
        }

        private LogEntry First => new LogEntry(nullSegment, sharedBuffer, new LogEntryMetadata());

        private async ValueTask<TResult> ReadEntriesImplAsync<TReader, TResult>(TReader reader, long startIndex, long endIndex, CancellationToken token)
            where TReader : ILogEntryReader<IRaftLogEntry, TResult>
        {
            if (startIndex > state.LastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            if (endIndex > state.LastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            var length = endIndex - startIndex + 1L;
            if (length > int.MaxValue)
                throw new InternalBufferOverflowException(ExceptionMessages.RangeTooBig);
            LogEntry entry;
            if (partitionTable.Count > 0)
                using (var list = new ArrayRental<LogEntry>(entryPool, (int)length))
                {
                    int listIndex;
                    for (listIndex = 0; startIndex <= endIndex; list[listIndex++] = entry, startIndex++)
                        if (startIndex == 0L)   //handle ephemeral entity
                            entry = First;
                        else if (TryGetPartition(startIndex, out var partition)) //handle regular record
                            entry = (await partition.ReadAsync(startIndex, true, token).ConfigureAwait(false)).Value;
                        else if (snapshot.Length > 0 && startIndex <= state.CommitIndex)    //probably the record is snapshotted
                        {
                            entry = await snapshot.ReadAsync(token).ConfigureAwait(false);
                            //skip squashed log entries
                            startIndex = state.CommitIndex - (state.CommitIndex + 1) % recordsPerPartition;
                        }
                        else
                            break;

                    return await reader.ReadAsync<LogEntry, ArraySegment<LogEntry>>(new ArraySegment<LogEntry>(list, 0, listIndex), list[0].SnapshotIndex, token);
                }
            else if (snapshot.Length > 0)
            {
                entry = await snapshot.ReadAsync(token).ConfigureAwait(false);
                return await reader.ReadAsync<LogEntry, SingletonEntryList>(new SingletonEntryList(entry), entry.SnapshotIndex, token);
            }
            else
                return await (startIndex == 0L ? reader.ReadAsync<LogEntry, SingletonEntryList>(new SingletonEntryList(First), null, token) : reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token)).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets log entries in the specified range.
        /// </summary>
        /// <remarks>
        /// This method may return less entries than <c>endIndex - startIndex + 1</c>. It may happen if the requested entries are committed entries and squashed into the single entry called snapshot.
        /// In this case the first entry in the collection is a snapshot entry. Additionally, the caller must call <see cref="IDisposable.Dispose"/> to release resources associated
        /// with the audit trail segment with entries.
        /// </remarks>
        /// <typeparam name="TReader">The type of the reader.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="endIndex"/> is negative.</exception>
        /// <exception cref="IndexOutOfRangeException"><paramref name="endIndex"/> is greater than the index of the last added entry.</exception>
        public async ValueTask<TResult> ReadEntriesAsync<TReader, TResult>(TReader reader, long startIndex, long endIndex, CancellationToken token)
            where TReader : ILogEntryReader<IRaftLogEntry, TResult>
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return await reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token).ConfigureAwait(false);
            using (await syncRoot.AcquireLockAsync(token).ConfigureAwait(false))
                return await ReadEntriesImplAsync<TReader, TResult>(reader, startIndex, endIndex, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets log entries starting from the specified index to the last log entry.
        /// </summary>
        /// <typeparam name="TReader">The type of the reader.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is negative.</exception>
        public async ValueTask<TResult> ReadEntriesAsync<TReader, TResult>(TReader reader, long startIndex, CancellationToken token)
            where TReader : ILogEntryReader<IRaftLogEntry, TResult>
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            using (await syncRoot.AcquireLockAsync(token).ConfigureAwait(false))
                return await ReadEntriesImplAsync<TReader, TResult>(reader, startIndex, state.LastIndex, token).ConfigureAwait(false);
        }

        private void RemovePartitions(IDictionary<long, Partition> partitions)
        {
            foreach (var (partitionNumber, partition) in partitions)
            {
                partitionTable.Remove(partitionNumber);
                var fileName = partition.Name;
                partition.Dispose();
                File.Delete(fileName);
            }
        }

        private async ValueTask InstallSnapshot(IRaftLogEntry snapshot, long snapshotIndex)
        {
            //0. The snapshot can be installed only if the partitions were squashed on the sender side
            //therefore, snapshotIndex should be a factor of recordsPerPartition
            if ((snapshotIndex + 1) % recordsPerPartition != 0)
                throw new ArgumentOutOfRangeException(nameof(snapshotIndex));
            //1. Save the snapshot into temporary file to avoid corruption caused by network connection
            string tempSnapshotFile, snapshotFile = this.snapshot.Name;
            using (var tempSnapshot = new Snapshot(location, sharedBuffer, true))
            {
                tempSnapshotFile = tempSnapshot.Name;
                await tempSnapshot.WriteAsync(snapshot, snapshotIndex, CancellationToken.None).ConfigureAwait(false);
            }
            //2. Delete existing snapshot file
            this.snapshot.Dispose();
            /*
             * Swapping snapshot file is unsafe operation because of potential disk I/O failures.
             * However, event if swapping will fail then it can be recovered manually just by renaming 'snapshot.new' file
             * into 'snapshot'. Both versions of snapshot file stay consistent. That's why stream copying is not an option.
             */
            try
            {
                File.Delete(snapshotFile);
                File.Move(tempSnapshotFile, snapshotFile);
            }
            catch (Exception e)
            {
                Environment.FailFast(LogMessages.SnapshotInstallationFailed, e);
            }
            this.snapshot = new Snapshot(location, sharedBuffer);
            this.snapshot.PopulateCache();
            //3. Identify all partitions to be replaced by snapshot
            var compactionScope = new Dictionary<long, Partition>();
            foreach (var (partitionNumber, partition) in partitionTable)
                if (partition.LastIndex <= snapshotIndex)
                    compactionScope.Add(partitionNumber, partition);
                else
                    break;  //enumeration is sorted by partition number so we don't need to enumerate over all partitions
            //4. Delete these partitions
            RemovePartitions(compactionScope);
            compactionScope.Clear();
            //5. Apply snapshot to the underlying state machine
            state.CommitIndex = snapshotIndex;
            state.LastIndex = Math.Max(snapshotIndex, state.LastIndex);
            await ApplyAsync(await this.snapshot.ReadAsync(CancellationToken.None).ConfigureAwait(false));
            state.LastApplied = snapshotIndex;
            state.Flush();
            commitEvent.Set(true);
        }

        //TODO: Should be replaced with IAsyncEnumerator in .NET Standard 2.1
        private async ValueTask AppendAsync(Func<ValueTask<IRaftLogEntry>> supplier, long startIndex, bool skipCommitted, CancellationToken token)
        {
            if (startIndex > state.LastIndex + 1)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            for (var entry = await supplier().ConfigureAwait(false); entry != null; state.LastIndex = startIndex++, token.ThrowIfCancellationRequested(), entry = await supplier().ConfigureAwait(false))
                if (entry.IsSnapshot)
                    throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);
                else if (startIndex > state.CommitIndex)
                    await GetOrCreatePartition(startIndex).WriteAsync(entry, startIndex).ConfigureAwait(false);
                else if (!skipCommitted)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            //flush updated state
            state.Flush();
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync(Func<ValueTask<IRaftLogEntry>> supplier, long startIndex, bool skipCommitted, CancellationToken token)
        {
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
                await AppendAsync(supplier, startIndex, skipCommitted, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This is the only method that can be used for snapshot installation.
        /// The behavior of the method depends on the <see cref="ILogEntry.IsSnapshot"/> property.
        /// If log entry is a snapshot then the method erases all committed log entries prior to <paramref name="startIndex"/>.
        /// If it is not, the method behaves in the same way as <see cref="AppendAsync(IReadOnlyList{IRaftLogEntry}, long, bool, CancellationToken)"/>.
        /// </remarks>
        /// <param name="entry">The uncommitted log entry to be added into this audit trail.</param>
        /// <param name="startIndex">The index of the </param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="entry"/> is not a snapshot.</exception>
        public async ValueTask AppendAsync(IRaftLogEntry entry, long startIndex)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                else if (entry.IsSnapshot)
                    await InstallSnapshot(entry, startIndex).ConfigureAwait(false);
                else if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                else if (startIndex > state.LastIndex + 1)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));
                else
                {
                    var partition = GetOrCreatePartition(startIndex);
                    await partition.WriteAsync(entry, startIndex).ConfigureAwait(false);
                    state.LastIndex = startIndex;
                    state.Flush();
                }
        }

        /// <summary>
        /// Adds uncommitted log entries into this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with new entries.</param>
        /// <param name="skipCommitted"><see langword="true"/> to skip committed entries from <paramref name="entries"/> instead of throwing exception.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry.</exception>
        public async ValueTask AppendAsync(IReadOnlyList<IRaftLogEntry> entries, long startIndex, bool skipCommitted = false, CancellationToken token = default)
        {
            if (entries.Count == 0)
                return;
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
            using (var enumerator = entries.GetEnumerator())
                await AppendAsync(enumerator.Advance, startIndex, skipCommitted, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds uncommitted log entries to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Index of the first added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
        public async ValueTask<long> AppendAsync(IReadOnlyList<IRaftLogEntry> entries, CancellationToken token = default)
        {
            if (entries.Count == 0)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty, nameof(entries));
            using (await syncRoot.AcquireLockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var startIndex = state.LastIndex + 1L;
                using (var enumerator = entries.GetEnumerator())
                    await AppendAsync(enumerator.Advance, startIndex, false, token).ConfigureAwait(false);
                return startIndex;
            }
        }

        /// <summary>
        /// Waits for the commit.
        /// </summary>
        /// <param name="index">The index of the log record to be committed.</param>
        /// <param name="timeout">The timeout used to wait for the commit.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns>The task representing waiting operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        public Task WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => index >= 0L ? CommitEvent.WaitForCommitAsync(this, commitEvent, index, timeout, token) : Task.FromException(new ArgumentOutOfRangeException(nameof(index)));

        /// <summary>
        /// Creates a new snapshot builder.
        /// </summary>
        /// <returns>The snapshot builder; or <see langword="null"/> if snapshotting is not supported.</returns>
        protected virtual SnapshotBuilder CreateSnapshotBuilder() => null;

        private async ValueTask ForceCompaction(SnapshotBuilder builder, CancellationToken token)
        {
            //1. Find the partitions that can be compacted
            var compactionScope = new SortedDictionary<long, Partition>();
            foreach (var (partNumber, partition) in partitionTable)
            {
                token.ThrowIfCancellationRequested();
                if (partition.LastIndex <= state.CommitIndex)
                    compactionScope.Add(partNumber, partition);
                else
                    break;  //enumeration is sorted by partition number so we don't need to enumerate over all partitions
            }
            Debug.Assert(compactionScope.Count > 0);
            //2. Do compaction
            var snapshotIndex = 0L;
            foreach (var partition in compactionScope.Values)
            {
                for (var i = 0L; i < partition.Capacity; i++)
                    if (partition.FirstIndex > 0L || i > 0L) //ignore the ephemeral entry
                    {
                        var entry = (await partition.ReadAsync(i, false, token).ConfigureAwait(false)).Value;
                        entry.AdjustPosition();
                        await builder.ApplyCoreAsync(entry).ConfigureAwait(false);
                    }
                snapshotIndex = partition.LastIndex;
            }
            //3. Persist snapshot
            await snapshot.WriteAsync(builder, snapshotIndex, token).ConfigureAwait(false);
            //4. Remove snapshotted partitions
            RemovePartitions(compactionScope);
            compactionScope.Clear();
        }

        private ValueTask ForceCompaction(CancellationToken token)
        {
            SnapshotBuilder builder;
            if (state.CommitIndex - snapshot.Index > recordsPerPartition && (builder = CreateSnapshotBuilder()) != null)
                try
                {
                    return ForceCompaction(builder, token);
                }
                finally
                {
                    builder.Dispose();
                }
            else
                return default;
        }

        private async ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
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
                    commitEvent.Set(true);
                }
            }
            return Math.Max(count, 0L);
        }

        /// <summary>
        /// Commits log entries into the underlying storage and marks these entries as committed.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="true"/>.
        /// Additionally, it may force log compaction and squash all committed entries into single entry called snapshot.
        /// </remarks>
        /// <param name="endIndex">The index of the last entry to commit, inclusively; if <see langword="null"/> then commits all log entries started from the first uncommitted entry to the last existing log entry.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of committed entries.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        public ValueTask<long> CommitAsync(long endIndex, CancellationToken token) => CommitAsync(new long?(endIndex), token);

        /// <summary>
        /// Commits log entries into the underlying storage and marks these entries as committed.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="true"/>.
        /// Additionally, it may force log compaction and squash all committed entries into single entry called snapshot.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of committed entries.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        public ValueTask<long> CommitAsync(CancellationToken token) => CommitAsync(null, token);

        /// <summary>
        /// Applies the command represented by the log entry to the underlying database engine.
        /// </summary>
        /// <remarks>
        /// The base method does nothing so you don't need to call base implementation.
        /// </remarks>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask ApplyAsync(LogEntry entry) => new ValueTask();

        private async ValueTask ApplyAsync(CancellationToken token)
        {
            for (var i = state.LastApplied + 1L; i <= state.CommitIndex; state.LastApplied = i++)
                if (TryGetPartition(i, out var partition))
                {
                    var entry = (await partition.ReadAsync(i, true, token).ConfigureAwait(false)).Value;
                    entry.AdjustPosition();
                    await ApplyAsync(entry).ConfigureAwait(false);
                }
                else
                    Debug.Fail($"Log entry with index {i} doesn't have partition");
            state.Flush();
        }

        /// <summary>
        /// Ensures that all committed entries are applied to the underlying data state machine known as database engine.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        public async Task EnsureConsistencyAsync(CancellationToken token)
        {
            using (await syncRoot.AcquireLockAsync(token).ConfigureAwait(false))
                await ApplyAsync(token).ConfigureAwait(false);
        }

        ref readonly IRaftLogEntry IAuditTrail<IRaftLogEntry>.First => ref initialEntry;

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
                syncRoot.Dispose();
                snapshot?.Dispose();
                nullSegment.Dispose();
                snapshot = null;
            }
            base.Dispose(disposing);
        }
    }
}