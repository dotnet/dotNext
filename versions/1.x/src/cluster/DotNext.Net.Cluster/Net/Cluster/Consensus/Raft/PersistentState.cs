using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

            private LogEntryMetadata(DateTimeOffset timeStamp, long term, long offset, long length)
            {
                Term = term;
                Timestamp = timeStamp.UtcTicks;
                Length = length;
                Offset = offset;
            }

            internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, long offset, long length)
                where TLogEntry : IRaftLogEntry
                => new LogEntryMetadata(entry.Timestamp, entry.Term, offset, length);

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

            private SnapshotMetadata(LogEntryMetadata metadata, long index)
            {
                Index = index;
                RecordMetadata = metadata;
            }

            internal static SnapshotMetadata Create<TLogEntry>(TLogEntry snapshot, long index, long length)
                where TLogEntry : IRaftLogEntry
                => new SnapshotMetadata(LogEntryMetadata.Create(snapshot, Size, length), index);

            internal static unsafe int Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => sizeof(SnapshotMetadata);
            }
        }

        /// <summary>
        /// Represents persistent log entry.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
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

            /// <summary>
            /// Gets a value indicating that this log entry is snapshot entry.
            /// </summary>
            public bool IsSnapshot => SnapshotIndex.HasValue;

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
            public ValueTask CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask(AdjustPosition().CopyToAsync(output, token));

            long? IDataTransferObject.Length => Length;
            bool IDataTransferObject.IsReusable => false;

            /// <summary>
            /// Gets Raft term of this log entry.
            /// </summary>
            public long Term => metadata.Term;

            /// <summary>
            /// Gets timestamp of this log entry.
            /// </summary>
            public DateTimeOffset Timestamp => new DateTimeOffset(metadata.Timestamp, TimeSpan.Zero);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct DataAccessSession : IDisposable
        {
            internal readonly int SessionId;
            private readonly ArrayPool<byte> bufferPool;
            internal readonly byte[] Buffer;

            //read session ctor
            internal DataAccessSession(int sessionId, ArrayPool<byte> bufferPool, int bufferSize)
            {
                SessionId = sessionId;
                Buffer = bufferPool.Rent(bufferSize);
                this.bufferPool = bufferPool;
            }

            //write session ctor
            internal DataAccessSession(byte[] buffer)
            {
                SessionId = 0;
                Buffer = buffer;
                bufferPool = null;
            }

            internal bool IsWriteSession => bufferPool is null;

            public void Dispose() => bufferPool?.Return(Buffer);
        }

        /*
         * This class helps to organize thread-safe concurrent access to the multiple streams
         * used for reading log entries. Such approach allows to use one-writer multiple-reader scenario
         * which dramatically improves the performance
         */
        [StructLayout(LayoutKind.Auto)]
        private readonly struct ReadSessionManager : IDisposable
        {
            private readonly ConcurrentBag<int> tokens;
            internal readonly int Capacity;
            private readonly ArrayPool<byte> bufferPool;
            internal readonly DataAccessSession WriteSession;

            internal ReadSessionManager(int readersCount, bool useSharedPool, DataAccessSession writeSession)
            {
                Capacity = readersCount;
                if (readersCount == 1)
                {
                    tokens = null;
                    bufferPool = null;
                }
                else
                {
                    tokens = new ConcurrentBag<int>(Enumerable.Range(0, readersCount));
                    bufferPool = useSharedPool ? ArrayPool<byte>.Shared : ArrayPool<byte>.Create();
                }
                WriteSession = writeSession;
            }

            internal DataAccessSession OpenSession(int bufferSize)
            {
                if (tokens is null)
                    return WriteSession;
                if (tokens.TryTake(out var sessionId))
                    return new DataAccessSession(sessionId, bufferPool, bufferSize);
                //never happens
                throw new InternalBufferOverflowException(ExceptionMessages.NoAvailableReadSessions);
            }

            internal void CloseSession(in DataAccessSession readSession)
            {
                if (tokens is null)
                    return;
                tokens.Add(readSession.SessionId);
                readSession.Dispose();
            }

            public void Dispose() => WriteSession.Dispose();
        }

        private abstract class ConcurrentStorageAccess : FileStream
        {
            private readonly StreamSegment[] readers;   //a pool of read-only streams that can be shared between multiple readers in parallel

            [SuppressMessage("Reliability", "CA2000", Justification = "All streams are disposed in Dispose method")]
            private protected ConcurrentStorageAccess(string fileName, int bufferSize, int readersCount, FileOptions options)
                : base(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, bufferSize, options)
            {
                readers = new StreamSegment[readersCount];
                if (readersCount == 1)
                    readers[0] = new StreamSegment(this, true);
                else
                    foreach (ref var reader in readers.AsSpan())
                        reader = new StreamSegment(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous | FileOptions.RandomAccess), false);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private protected StreamSegment GetReadSessionStream(in DataAccessSession session) => readers[session.SessionId];

            internal Task FlushAsync(in DataAccessSession session, CancellationToken token)
                => GetReadSessionStream(session).FlushAsync(token);

            internal abstract void PopulateCache(in DataAccessSession session);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    foreach (ref var reader in readers.AsSpan())
                    {
                        reader.Dispose();
                        reader = null;
                    }
                base.Dispose(disposing);
            }
        }

        /*
            Partition file format:
            FileName - number of partition
            Allocation table:
            [struct LogEntryMetadata] X number of entries
            Payload:
            [octet string] X number of entries
         */
        private sealed class Partition : ConcurrentStorageAccess
        {
            internal readonly long FirstIndex;
            internal readonly int Capacity;    //max number of entries
            private readonly ArrayRental<LogEntryMetadata> lookupCache;

            internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, ArrayPool<LogEntryMetadata> cachePool, int readersCount)
                : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), bufferSize, readersCount, FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous)
            {
                Capacity = recordsPerPartition;
                FirstIndex = partitionNumber * recordsPerPartition;
                lookupCache = cachePool is null ? default : new ArrayRental<LogEntryMetadata>(cachePool, recordsPerPartition);
            }

            private long PayloadOffset => LogEntryMetadata.Size * (long)Capacity;

            internal long LastIndex => FirstIndex + Capacity - 1;

            internal void Allocate(long initialSize) => SetLength(initialSize + PayloadOffset);

            private void PopulateCache(byte[] buffer, Span<LogEntryMetadata> lookupCache)
            {
                for (int index = 0, count; index < lookupCache.Length; index += count)
                {
                    count = Math.Min(buffer.Length / LogEntryMetadata.Size, lookupCache.Length - index);
                    var maxBytes = count * LogEntryMetadata.Size;
                    if (Read(buffer, 0, maxBytes) < maxBytes)
                        throw new EndOfStreamException();
                    var source = new Span<byte>(buffer, 0, maxBytes);
                    var destination = MemoryMarshal.AsBytes(lookupCache.Slice(index));
                    source.CopyTo(destination);
                }
            }

            internal override void PopulateCache(in DataAccessSession session)
            {
                if (!lookupCache.IsEmpty)
                    PopulateCache(session.Buffer, lookupCache.Memory.Span);
            }

            private async ValueTask<LogEntry?> ReadAsync(StreamSegment reader, byte[] buffer, long index, bool absoluteIndex, bool refreshStream, CancellationToken token)
            {
                //calculate relative index
                if (absoluteIndex)
                    index -= FirstIndex;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");
                if (refreshStream)
                    await reader.FlushAsync(token).ConfigureAwait(false);
                //find pointer to the content
                LogEntryMetadata metadata;
                if (lookupCache.IsEmpty)
                {
                    reader.BaseStream.Position = index * LogEntryMetadata.Size;
                    metadata = await reader.BaseStream.ReadAsync<LogEntryMetadata>(buffer, token).ConfigureAwait(false);
                }
                else
                    metadata = lookupCache[index];
                return metadata.Offset > 0 ? new LogEntry(reader, buffer, metadata) : new LogEntry?();
            }

            internal ValueTask<LogEntry?> ReadAsync(in DataAccessSession session, long index, bool absoluteIndex, bool refreshStream, CancellationToken token)
                => ReadAsync(GetReadSessionStream(session), session.Buffer, index, absoluteIndex, refreshStream, token);

            private async ValueTask WriteAsync<TEntry>(TEntry entry, long index, byte[] buffer)
                where TEntry : IRaftLogEntry
            {
                //calculate relative index
                index -= FirstIndex;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");
                //calculate offset of the previous entry
                long offset;
                LogEntryMetadata metadata;
                if (index == 0L || index == 1L && FirstIndex == 0L)
                    offset = PayloadOffset;
                else if (lookupCache.IsEmpty)
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
                metadata = LogEntryMetadata.Create(entry, offset, Position - offset);
                //record new log entry to the allocation table
                Position = index * LogEntryMetadata.Size;
                await this.WriteAsync(ref metadata, buffer).ConfigureAwait(false);
                //update cache
                if (!lookupCache.IsEmpty)
                    lookupCache[index] = metadata;
            }

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long index)
                where TEntry : IRaftLogEntry
                => WriteAsync(entry, index, session.Buffer);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    lookupCache.Dispose();
                base.Dispose(disposing);
            }
        }

        /*
         * Binary format:
         * [struct SnapshotMetadata] X 1
         * [octet string] X 1
         */
        private sealed class Snapshot : ConcurrentStorageAccess
        {
            private const string FileName = "snapshot";
            private const string TempFileName = "snapshot.new";

            internal Snapshot(DirectoryInfo location, int bufferSize, int readersCount, bool tempSnapshot = false)
                : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), bufferSize, readersCount, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess | FileOptions.WriteThrough)
            {
            }

            internal override void PopulateCache(in DataAccessSession session)
                => Index = Length > 0L ? this.Read<SnapshotMetadata>(session.Buffer).Index : 0L;

            private async ValueTask WriteAsync<TEntry>(TEntry entry, long index, byte[] buffer, CancellationToken token)
                where TEntry : IRaftLogEntry
            {
                Index = index;
                Position = SnapshotMetadata.Size;
                await entry.CopyToAsync(this, token).ConfigureAwait(false);
                var metadata = SnapshotMetadata.Create(entry, index, Length - SnapshotMetadata.Size);
                Position = 0;
                await this.WriteAsync(ref metadata, buffer, token).ConfigureAwait(false);
            }

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long index, CancellationToken token)
                where TEntry : IRaftLogEntry
                => WriteAsync(entry, index, session.Buffer, token);

            private static async ValueTask<LogEntry> ReadAsync(StreamSegment reader, byte[] buffer, CancellationToken token)
            {
                reader.BaseStream.Position = 0;
                //snapshot reader stream may be out of sync with writer stream
                await reader.FlushAsync(token).ConfigureAwait(false);
                return new LogEntry(reader, buffer, await reader.BaseStream.ReadAsync<SnapshotMetadata>(buffer, token).ConfigureAwait(false));
            }

            internal ValueTask<LogEntry> ReadAsync(in DataAccessSession session, CancellationToken token)
                => ReadAsync(GetReadSessionStream(session), session.Buffer, token);

            //cached index of the snapshotted entry
            internal long Index
            {
                get;
                private set;
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
            private AsyncLock syncRoot;
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
                    syncRoot = default;
                    votedFor = null;
                }
                base.Dispose(disposing);
            }
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

        /// <summary>
        /// Represents configuration options of the persistent audit trail.
        /// </summary>
        public class Options
        {
            private const int MinBufferSize = 128;
            private int bufferSize = 2048;
            private int concurrencyLevel = 3;

            /// <summary>
            /// Gets size of in-memory buffer for I/O operations.
            /// </summary>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is too small.</exception>
            public int BufferSize
            {
                get => bufferSize;
                set
                {
                    if (value < MinBufferSize)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    bufferSize = value;
                }
            }

            /// <summary>
            /// Gets or sets the initial size of the file that holds the partition with log entries.
            /// </summary>
            public long InitialPartitionSize { get; set; } = 0;

            /// <summary>
            /// Enables or disables in-memory cache.
            /// </summary>
            /// <value><see langword="true"/> to in-memory cache for faster read/write of log entries; <see langword="false"/> to reduce the memory by the cost of the performance.</value>
            public bool UseCaching { get; set; } = true;

            /// <summary>
            /// Gets or sets value indicating usage policy of array pools.
            /// </summary>
            /// <value><see langword="true"/> to use <see cref="ArrayPool{T}.Shared"/> pool for internal purposes; <see langword="false"/> to use dedicated pool of arrays.</value>
            public bool UseSharedPool { get; set; } = true;

            /// <summary>
            /// Gets or sets the number of possible parallel reads.
            /// </summary>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 1.</exception>
            public int MaxConcurrentReads
            {
                get => concurrencyLevel;
                set
                {
                    if (concurrencyLevel < 1)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    concurrencyLevel = value;
                }
            }
        }

        private readonly int recordsPerPartition;
        //key is the number of partition
        private readonly IDictionary<long, Partition> partitionTable;
        private readonly NodeState state;
        private Snapshot snapshot;
        private readonly DirectoryInfo location;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly AsyncSharedLock syncRoot;
        private readonly IRaftLogEntry initialEntry;
        private readonly long initialSize;
        private readonly ArrayPool<LogEntry> entryPool;
        private readonly ArrayPool<LogEntryMetadata> metadataPool;
        private readonly StreamSegment nullSegment;
        //concurrent read sessions management
        private readonly ReadSessionManager sessionManager;
        private readonly int bufferSize;

        /// <summary>
        /// Initializes a new persistent audit trail.
        /// </summary>
        /// <param name="path">The path to the folder to be used by audit trail.</param>
        /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
        /// <param name="configuration">The configuration of the persistent audit trail.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
        public PersistentState(DirectoryInfo path, int recordsPerPartition, Options configuration = null)
        {
            if (configuration is null)
                configuration = new Options();
            if (recordsPerPartition < 2L)
                throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
            if (!path.Exists)
                path.Create();
            bufferSize = configuration.BufferSize;
            location = path;
            this.recordsPerPartition = recordsPerPartition;
            initialSize = configuration.InitialPartitionSize;
            commitEvent = new AsyncManualResetEvent(false);
            sessionManager = new ReadSessionManager(configuration.MaxConcurrentReads, configuration.UseSharedPool, new DataAccessSession(new byte[bufferSize]));
            syncRoot = new AsyncSharedLock(sessionManager.Capacity);
            if (configuration.UseSharedPool)
            {
                entryPool = ArrayPool<LogEntry>.Shared;
                metadataPool = configuration.UseCaching ? ArrayPool<LogEntryMetadata>.Shared : null;
            }
            else
            {
                entryPool = ArrayPool<LogEntry>.Create();
                metadataPool = configuration.UseCaching ? ArrayPool<LogEntryMetadata>.Create() : null;
            }
            nullSegment = new StreamSegment(Stream.Null);
            initialEntry = new LogEntry(nullSegment, sessionManager.WriteSession.Buffer, new LogEntryMetadata());
            //sorted dictionary to improve performance of log compaction and snapshot installation procedures
            partitionTable = new SortedDictionary<long, Partition>();
            //load all partitions from file system
            foreach (var file in path.EnumerateFiles())
                if (long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.Directory, bufferSize, recordsPerPartition, partitionNumber, metadataPool, sessionManager.Capacity);
                    partition.PopulateCache(sessionManager.WriteSession);
                    partitionTable[partitionNumber] = partition;
                }
            state = new NodeState(path, AsyncLock.Exclusive(syncRoot));
            snapshot = new Snapshot(path, bufferSize, sessionManager.Capacity);
            snapshot.PopulateCache(sessionManager.WriteSession);
        }

        /// <summary>
        /// Initializes a new persistent audit trail.
        /// </summary>
        /// <param name="path">The path to the folder to be used by audit trail.</param>
        /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
        /// <param name="configuration">The configuration of the persistent audit trail.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
        public PersistentState(string path, int recordsPerPartition, Options configuration = null)
            : this(new DirectoryInfo(path), recordsPerPartition, configuration)
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

        /// <summary>
        /// Gets the buffer that can be used to perform I/O operations.
        /// </summary>
        /// <remarks>
        /// The buffer cannot be used concurrently. Access to it should be synchronized
        /// using <see cref="SyncRoot"/> property.
        /// </remarks>
        [SuppressMessage("Performance", "CA1819", Justification = "Buffer is shared across write operations")]
        protected byte[] Buffer => sessionManager.WriteSession.Buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetPartition(long recordIndex, ref Partition partition)
            => partition != null && recordIndex >= partition.FirstIndex && recordIndex <= partition.LastIndex || partitionTable.TryGetValue(PartitionOf(recordIndex), out partition);

        private bool TryGetPartition(long recordIndex, ref Partition partition, out bool switched)
        {
            var previous = partition;
            var result = TryGetPartition(recordIndex, ref partition);
            switched = partition != previous;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Task FlushAsync(Partition partition) => partition is null ? Task.CompletedTask : partition.FlushAsync();

        private void GetOrCreatePartition(long recordIndex, out Partition partition)
        {
            var partitionNumber = PartitionOf(recordIndex);
            if (!partitionTable.TryGetValue(partitionNumber, out partition))
            {
                partition = new Partition(location, Buffer.Length, recordsPerPartition, partitionNumber, metadataPool, sessionManager.Capacity);
                partition.Allocate(initialSize);
                partitionTable.Add(partitionNumber, partition);
            }
        }

        private Task GetOrCreatePartitionAsync(long recordIndex, ref Partition partition)
        {
            Task flushTask;
            if (partition is null || recordIndex < partition.FirstIndex || recordIndex > partition.LastIndex)
            {
                flushTask = FlushAsync(partition);
                GetOrCreatePartition(recordIndex, out partition);
            }
            else
                flushTask = Task.CompletedTask;
            return flushTask;
        }

        private LogEntry First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.Unbox<LogEntry>(initialEntry);
        }

        private async ValueTask<TResult> ReadAsync<TReader, TResult>(TReader reader, DataAccessSession session, long startIndex, long endIndex, CancellationToken token)
            where TReader : ILogEntryConsumer<IRaftLogEntry, TResult>
        {
            if (startIndex > state.LastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            if (endIndex > state.LastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            var length = endIndex - startIndex + 1L;
            if (length > int.MaxValue)
                throw new InternalBufferOverflowException(ExceptionMessages.RangeTooBig);
            LogEntry entry;
            ValueTask<TResult> result;
            if (partitionTable.Count > 0)
                using (var list = new ArrayRental<LogEntry>(entryPool, (int)length))
                {
                    var listIndex = 0;
                    for (Partition partition = null; startIndex <= endIndex; list[listIndex++] = entry, startIndex++)
                        if (startIndex == 0L)   //handle ephemeral entity
                            entry = First;
                        else if (TryGetPartition(startIndex, ref partition, out var switched)) //handle regular record
                            entry = (await partition.ReadAsync(session, startIndex, true, switched, token).ConfigureAwait(false)).Value;
                        else if (snapshot.Length > 0 && startIndex <= state.CommitIndex)    //probably the record is snapshotted
                        {
                            entry = await snapshot.ReadAsync(session, token).ConfigureAwait(false);
                            //skip squashed log entries
                            startIndex = state.CommitIndex - (state.CommitIndex + 1) % recordsPerPartition;
                        }
                        else
                            break;
                    result = reader.ReadAsync<LogEntry, ArraySegment<LogEntry>>(list.Slice(0, listIndex), list[0].SnapshotIndex, token);
                }
            else if (snapshot.Length > 0)
            {
                entry = await snapshot.ReadAsync(session, token).ConfigureAwait(false);
                result = reader.ReadAsync<LogEntry, SingletonEntryList<LogEntry>>(new SingletonEntryList<LogEntry>(entry), entry.SnapshotIndex, token);
            }
            else
                result = startIndex == 0L ? reader.ReadAsync<LogEntry, SingletonEntryList<LogEntry>>(new SingletonEntryList<LogEntry>(First), null, token) : reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
            return await result.ConfigureAwait(false);
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
        public async ValueTask<TResult> ReadAsync<TReader, TResult>(TReader reader, long startIndex, long endIndex, CancellationToken token)
            where TReader : ILogEntryConsumer<IRaftLogEntry, TResult>
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return await reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token).ConfigureAwait(false);
            //obtain weak lock as read lock
            await syncRoot.Acquire(false, token).ConfigureAwait(false);
            var session = sessionManager.OpenSession(bufferSize);
            try
            {
                return await ReadAsync<TReader, TResult>(reader, session, startIndex, endIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.CloseSession(session);  //return session back to the pool
                syncRoot.Release();
            }
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
        public async ValueTask<TResult> ReadAsync<TReader, TResult>(TReader reader, long startIndex, CancellationToken token)
            where TReader : ILogEntryConsumer<IRaftLogEntry, TResult>
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            await syncRoot.Acquire(false, token).ConfigureAwait(false);
            var session = sessionManager.OpenSession(bufferSize);
            try
            {
                return await ReadAsync<TReader, TResult>(reader, session, startIndex, state.LastIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.CloseSession(session);
                syncRoot.Release();
            }
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

        private async ValueTask InstallSnapshot<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
            where TSnapshot : IRaftLogEntry
        {
            //0. The snapshot can be installed only if the partitions were squashed on the sender side
            //therefore, snapshotIndex should be a factor of recordsPerPartition
            if ((snapshotIndex + 1) % recordsPerPartition != 0)
                throw new ArgumentOutOfRangeException(nameof(snapshotIndex));
            //1. Save the snapshot into temporary file to avoid corruption caused by network connection
            string tempSnapshotFile, snapshotFile = this.snapshot.Name;
            using (var tempSnapshot = new Snapshot(location, Buffer.Length, 0, true))
            {
                tempSnapshotFile = tempSnapshot.Name;
                await tempSnapshot.WriteAsync(sessionManager.WriteSession, snapshot, snapshotIndex, CancellationToken.None).ConfigureAwait(false);
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
            this.snapshot = new Snapshot(location, Buffer.Length, sessionManager.Capacity);
            this.snapshot.PopulateCache(sessionManager.WriteSession);
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

            await ApplyAsync(await this.snapshot.ReadAsync(sessionManager.WriteSession, CancellationToken.None).ConfigureAwait(false));
            state.LastApplied = snapshotIndex;
            state.Flush();
            await FlushAsync().ConfigureAwait(false);
            commitEvent.Set(true);
        }

        private async ValueTask AppendAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool skipCommitted, CancellationToken token)
            where TEntry : IRaftLogEntry
        {
            if (startIndex > state.LastIndex + 1)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            Partition partition;
            for (partition = null; !token.IsCancellationRequested && await supplier.MoveNextAsync().ConfigureAwait(false); state.LastIndex = startIndex++)
                if (supplier.Current.IsSnapshot)
                    throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);
                else if (startIndex > state.CommitIndex)
                {
                    await GetOrCreatePartitionAsync(startIndex, ref partition).ConfigureAwait(false);
                    await partition.WriteAsync(sessionManager.WriteSession, supplier.Current, startIndex).ConfigureAwait(false);
                }
                else if (!skipCommitted)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            await FlushAsync(partition).ConfigureAwait(false);
            //flush updated state
            state.Flush();
            token.ThrowIfCancellationRequested();
        }

        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
        {
            if (entries.RemainingCount == 0L)
                return;
            await syncRoot.Acquire(true, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await AppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This is the only method that can be used for snapshot installation.
        /// The behavior of the method depends on the <see cref="ILogEntry.IsSnapshot"/> property.
        /// If log entry is a snapshot then the method erases all committed log entries prior to <paramref name="startIndex"/>.
        /// If it is not, the method behaves in the same way as <see cref="AppendAsync{TEntryImpl}(ILogEntryProducer{TEntryImpl}, long, bool, CancellationToken)"/>.
        /// </remarks>
        /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
        /// <param name="entry">The uncommitted log entry to be added into this audit trail.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with the new entry.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="entry"/> is not a snapshot.</exception>
        public async ValueTask AppendAsync<TEntry>(TEntry entry, long startIndex)
            where TEntry : IRaftLogEntry
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            await syncRoot.Acquire(true, CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                else if (entry.IsSnapshot)
                    await InstallSnapshot(entry, startIndex).ConfigureAwait(false);
                else if (startIndex > state.LastIndex + 1)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));
                else
                {
                    GetOrCreatePartition(startIndex, out var partition);
                    await partition.WriteAsync(sessionManager.WriteSession, entry, startIndex).ConfigureAwait(false);
                    await partition.FlushAsync().ConfigureAwait(false);
                    state.LastIndex = startIndex;
                    state.Flush();
                }
            }
            finally
            {
                syncRoot.Release();
            }
        }

        /// <summary>
        /// Adds uncommitted log entries to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <typeparam name="TEntry">The actual type of the log entry returned by the supplier.</typeparam>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Index of the first added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The collection of entries contains the snapshot entry.</exception>
        public async ValueTask<long> AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, CancellationToken token = default)
            where TEntry : IRaftLogEntry
        {
            if (entries.RemainingCount == 0L)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty);
            await syncRoot.Acquire(true, token).ConfigureAwait(false);
            var startIndex = state.LastIndex + 1L;
            try
            {
                await AppendAsync(entries, startIndex, false, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.Release();
            }
            return startIndex;
        }

        /// <summary>
        /// Dropes the uncommitted entries starting from the specified position to the end of the log.
        /// </summary>
        /// <param name="startIndex">The index of the first log entry to be dropped.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of dropped entries.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> represents index of the committed entry.</exception>
        public async ValueTask<long> DropAsync(long startIndex, CancellationToken token)
        {
            long count;
            await syncRoot.Acquire(true, token).ConfigureAwait(false);
            try
            {
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                if (startIndex > state.LastIndex)
                    return 0L;
                count = state.LastIndex - startIndex + 1L;
                state.LastIndex = startIndex - 1L;
                state.Flush();
                //find partitions to be deleted
                var partitionNumber = Math.DivRem(startIndex, recordsPerPartition, out var remainder);
                //take the next partition if startIndex is not a beginning of the calculated partition
                partitionNumber += (remainder > 0L).ToInt32();
                for (Partition partition; partitionTable.TryGetValue(partitionNumber, out partition); partitionNumber++)
                {
                    var fileName = partition.Name;
                    partitionTable.Remove(partitionNumber);
                    partition.Dispose();
                    File.Delete(fileName);
                }
            }
            finally
            {
                syncRoot.Release();
            }
            return count;
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
                await partition.FlushAsync(sessionManager.WriteSession, token).ConfigureAwait(false);
                for (var i = 0; i < partition.Capacity; i++)
                    if (partition.FirstIndex > 0L || i > 0L) //ignore the ephemeral entry
                    {
                        var entry = (await partition.ReadAsync(sessionManager.WriteSession, i, false, false, token).ConfigureAwait(false)).Value;
                        entry.AdjustPosition();
                        await builder.ApplyCoreAsync(entry).ConfigureAwait(false);
                    }
                snapshotIndex = partition.LastIndex;
            }
            //3. Persist snapshot
            await snapshot.WriteAsync(sessionManager.WriteSession, builder, snapshotIndex, token).ConfigureAwait(false);
            await snapshot.FlushAsync().ConfigureAwait(false);
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
            await syncRoot.Acquire(true, token).ConfigureAwait(false);
            var startIndex = state.CommitIndex + 1L;
            try
            {
                count = (endIndex ?? GetLastIndex(false)) - startIndex + 1L;
                if (count > 0)
                {
                    state.CommitIndex = startIndex + count - 1;
                    await ApplyAsync(token).ConfigureAwait(false);
                    await ForceCompaction(token).ConfigureAwait(false);
                    commitEvent.Set(true);
                }
            }
            finally
            {
                syncRoot.Release();
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
        /// <param name="entry">The entry to be applied to the state machine.</param>
        /// <remarks>
        /// The base method does nothing so you don't need to call base implementation.
        /// </remarks>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask ApplyAsync(LogEntry entry) => new ValueTask();

        /// <summary>
        /// Flushes the underlying data storage.
        /// </summary>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask FlushAsync() => new ValueTask();

        private async ValueTask ApplyAsync(long startIndex, CancellationToken token)
        {
            for (Partition partition = null; startIndex <= state.CommitIndex; state.LastApplied = startIndex++)
                if (TryGetPartition(startIndex, ref partition, out var switched))
                {
                    var entry = (await partition.ReadAsync(sessionManager.WriteSession, startIndex, true, switched, token).ConfigureAwait(false)).Value;
                    entry.AdjustPosition();
                    await ApplyAsync(entry).ConfigureAwait(false);
                }
                else
                    Debug.Fail($"Log entry with index {startIndex} doesn't have partition");
            state.Flush();
            await FlushAsync().ConfigureAwait(false);
        }

        private ValueTask ApplyAsync(CancellationToken token)
            => ApplyAsync(state.LastApplied + 1L, token);

        /// <summary>
        /// Ensures that all committed entries are applied to the underlying data state machine known as database engine.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        [Obsolete("Use ReplayAsync to recover state correctly")]
        public async Task EnsureConsistencyAsync(CancellationToken token = default)
        {
            await syncRoot.Acquire(true, token).ConfigureAwait(false);
            try
            {
                await ApplyAsync(token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        /// <summary>
        /// Reconstucts dataset by calling <see cref="ApplyAsync(LogEntry)"/>
        /// for each committed entry.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        public async Task ReplayAsync(CancellationToken token = default)
        {
            await syncRoot.Acquire(true, token).ConfigureAwait(false);
            try
            {
                LogEntry entry;
                long startIndex;
                //1. Apply snapshot if not empty
                if (snapshot.Length > 0L)
                {
                    entry = await snapshot.ReadAsync(sessionManager.WriteSession, token).ConfigureAwait(false);
                    await ApplyAsync(entry).ConfigureAwait(false);
                    startIndex = snapshot.Index;
                }
                else
                    startIndex = 0L;
                //2. Apply all committed entries
                await ApplyAsync(startIndex + 1L, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.Release();
            }
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
                sessionManager.Dispose();
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