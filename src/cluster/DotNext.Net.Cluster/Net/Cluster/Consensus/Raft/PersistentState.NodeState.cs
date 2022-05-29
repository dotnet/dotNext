using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using Threading;
using BoxedClusterMemberId = Runtime.BoxedValue<ClusterMemberId>;
using IntegrityException = IO.Log.IntegrityException;
using Intrinsics = Runtime.Intrinsics;

public partial class PersistentState
{
    /*
        State file format:
        8 bytes = CommitIndex
        8 bytes = LastApplied
        8 bytes = LastIndex
        [SnapshotMetadata.Size] bytes = snapshot metadata
        8 bytes = Term
        1 byte = presence of cluster member id
        [ClusterMemberId.Size] bytes = last vote
     */
    private sealed class NodeState : Disposable
    {
        internal static readonly Func<NodeState, long, bool> IsCommittedPredicate = IsCommitted;

        private const string FileName = "node.state";
        private const byte False = 0;
        private const byte True = 1;
        private const int Capacity = 128;
        private const int CommitIndexOffset = 0;
        private const int LastAppliedOffset = CommitIndexOffset + sizeof(long);
        private const int LastIndexOffset = LastAppliedOffset + sizeof(long);
        private const int SnapshotMetadataOffset = LastIndexOffset + sizeof(long);
        private const int TermOffset = SnapshotMetadataOffset + SnapshotMetadata.Size;
        private const int LastVotePresenceOffset = TermOffset + sizeof(long);
        private const int LastVoteOffset = LastVotePresenceOffset + sizeof(byte);
        private const int ChecksumOffset = Capacity - sizeof(long);

        internal static readonly Range IndexesRange = CommitIndexOffset..SnapshotMetadataOffset,
                                        LastVoteRange = LastVotePresenceOffset.. (LastVoteOffset + ClusterMemberId.Size),
                                        TermAndLastVoteFlagRange = TermOffset..LastVoteOffset,
                                        TermAndLastVoteRange = TermOffset.. (LastVoteOffset + ClusterMemberId.Size),
                                        IndexesAndSnapshotRange = CommitIndexOffset..TermOffset,
                                        SnapshotRange = SnapshotMetadataOffset..TermOffset;

        private readonly SafeFileHandle handle;
        private readonly bool integrityCheck;
        private MemoryOwner<byte> buffer;

        // boxed ClusterMemberId or null if there is not last vote stored
        private volatile BoxedClusterMemberId? votedFor;
        private long term, commitIndex, lastIndex, lastApplied;  // volatile
        private SnapshotMetadata snapshot; // cached snapshot metadata to avoid backward writes

        private NodeState(string fileName, MemoryAllocator<byte> allocator, bool integrityCheck, bool writeThrough)
        {
            Debug.Assert(Capacity >= LastVoteOffset + sizeof(long));

            buffer = allocator.Invoke(Capacity, true);
            if (File.Exists(fileName))
            {
                handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
                RandomAccess.Read(handle, buffer.Span, 0L);
            }
            else
            {
                handle = File.OpenHandle(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.WriteThrough, Capacity);
                buffer.Span.Clear();
                if (integrityCheck)
                    WriteInt64LittleEndian(Checksum, Hash(Data));

                RandomAccess.Write(handle, buffer.Span, 0L);
            }

            // reopen handle in asynchronous mode
            handle.Dispose();

            const FileOptions dontSkipBuffer = FileOptions.Asynchronous | FileOptions.SequentialScan;
            const FileOptions skipBuffer = FileOptions.Asynchronous | FileOptions.WriteThrough | FileOptions.SequentialScan;
            handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, writeThrough ? skipBuffer : dontSkipBuffer);

            // restore state
            ReadOnlySpan<byte> bufferSpan = buffer.Span;
            term = ReadInt64LittleEndian(bufferSpan.Slice(TermOffset));
            commitIndex = ReadInt64LittleEndian(bufferSpan.Slice(CommitIndexOffset));
            lastIndex = ReadInt64LittleEndian(bufferSpan.Slice(LastIndexOffset));
            lastApplied = ReadInt64LittleEndian(bufferSpan.Slice(LastAppliedOffset));
            snapshot = new(bufferSpan.Slice(SnapshotMetadataOffset));
            if (ValueTypeExtensions.ToBoolean(bufferSpan[LastVotePresenceOffset]))
                votedFor = BoxedClusterMemberId.Box(new ClusterMemberId(bufferSpan.Slice(LastVoteOffset)));
            this.integrityCheck = integrityCheck;
        }

        internal NodeState(DirectoryInfo location, MemoryAllocator<byte> allocator, bool integrityCheck, bool writeThrough)
            : this(Path.Combine(location.FullName, FileName), allocator, integrityCheck, writeThrough)
        {
        }

        private ReadOnlySpan<byte> Data => buffer.Span[CommitIndexOffset..ChecksumOffset];

        private Span<byte> Checksum => buffer.Span.Slice(ChecksumOffset);

        internal bool VerifyIntegrity()
            => !integrityCheck || Hash(Data) == ReadInt64LittleEndian(Checksum);

        private static long Hash(ReadOnlySpan<byte> input)
        {
            // we're using FNV1a 64-bit version
            const long prime = 1099511628211;
            const long offset = unchecked((long)14695981039346656037);

            var hash = offset;
            var length = input.Length;
            ref byte ptr = ref MemoryMarshal.GetReference(input);

            // Perf: x % 2^n == x & (2^n - 1)
            const nint moduloOperand = sizeof(long) - 1;
            if ((Intrinsics.AddressOf(in ptr) & moduloOperand) is 0)
            {
                // pointer is aligned
                for (; length >= sizeof(long); length -= sizeof(long))
                {
                    hash = HashRound(hash, Unsafe.As<byte, long>(ref ptr));
                    ptr = ref Unsafe.Add(ref ptr, sizeof(long));
                }

                if (length >= sizeof(int))
                {
                    hash = HashRound(hash, Unsafe.As<byte, int>(ref ptr));
                    ptr = ref Unsafe.Add(ref ptr, sizeof(int));
                }
            }
            else
            {
                // pointer is unaligned
                for (; length >= sizeof(long); length -= sizeof(long))
                {
                    hash = HashRound(hash, Unsafe.ReadUnaligned<long>(ref ptr));
                    ptr = ref Unsafe.Add(ref ptr, sizeof(long));
                }

                if (length >= sizeof(int))
                {
                    hash = HashRound(hash, Unsafe.ReadUnaligned<int>(ref ptr));
                    ptr = ref Unsafe.Add(ref ptr, sizeof(int));
                }
            }

            // hash rest of the data
            for (; length > 0; length -= sizeof(byte))
            {
                hash = HashRound(hash, ptr);
                ptr = ref Unsafe.Add(ref ptr, 1);
            }

            return hash;

            static long HashRound(long hash, long data) => unchecked((hash ^ data) * prime);
        }

        internal ValueTask FlushAsync(in Range range)
        {
            ReadOnlyMemory<byte> data = buffer.Memory;
            int offset;

            if (integrityCheck)
            {
                WriteInt64LittleEndian(Checksum, Hash(Data));
                offset = 0;
            }
            else
            {
                (offset, var length) = range.GetOffsetAndLength(data.Length);
                data = data.Slice(offset, length);
            }

            return RandomAccess.WriteAsync(handle, data, offset);
        }

        internal ValueTask ClearAsync(CancellationToken token = default)
        {
            votedFor = null;
            snapshot = default;
            term = commitIndex = lastIndex = lastApplied = 0L;
            buffer.Span.Clear();
            return RandomAccess.WriteAsync(handle, buffer.Memory, 0L, token);
        }

        internal long CommitIndex
        {
            get => commitIndex.VolatileRead();
            set
            {
                WriteInt64LittleEndian(buffer.Span.Slice(CommitIndexOffset), value);
                commitIndex.VolatileWrite(value);
            }
        }

        private static bool IsCommitted(NodeState state, long index) => index <= state.CommitIndex;

        internal long LastApplied
        {
            get => lastApplied.VolatileRead();
            set
            {
                WriteInt64LittleEndian(buffer.Span.Slice(LastAppliedOffset), value);
                lastApplied.VolatileWrite(value);
            }
        }

        internal long LastIndex
        {
            get => lastIndex.VolatileRead();
            set
            {
                WriteInt64LittleEndian(buffer.Span.Slice(LastIndexOffset), value);
                lastIndex.VolatileWrite(value);
            }
        }

        internal long TailIndex => LastIndex + 1L;

        internal long Term => term.VolatileRead();

        internal void UpdateTerm(long value, bool resetLastVote)
        {
            WriteInt64LittleEndian(buffer.Span.Slice(TermOffset), value);
            if (resetLastVote)
            {
                votedFor = null;
                buffer[LastVotePresenceOffset] = False;
            }

            term.VolatileWrite(value);
        }

        internal long IncrementTerm(ClusterMemberId id)
        {
            var result = term.IncrementAndGet();
            WriteInt64LittleEndian(buffer.Span.Slice(TermOffset), result);
            UpdateVotedFor(id);
            return result;
        }

        internal bool IsVotedFor(in ClusterMemberId? expected) => IPersistentState.IsVotedFor(votedFor, expected);

        internal void UpdateVotedFor(ClusterMemberId? member)
        {
            if (member.HasValue)
            {
                UpdateVotedFor(member.GetValueOrDefault());
            }
            else
            {
                votedFor = null;
                buffer[LastVotePresenceOffset] = False;
            }
        }

        private void UpdateVotedFor(ClusterMemberId id)
        {
            votedFor = BoxedClusterMemberId.Box(id);
            buffer[LastVotePresenceOffset] = True;
#pragma warning disable CA2252 // TODO: Remove in .NET 7
            IBinaryFormattable<ClusterMemberId>.Format(id, buffer.Span.Slice(LastVoteOffset));
#pragma warning restore CA2252
        }

        internal ref readonly SnapshotMetadata Snapshot => ref snapshot;

        internal void UpdateSnapshotMetadata(in SnapshotMetadata metadata)
        {
            snapshot = metadata;
            metadata.Format(buffer.Span.Slice(SnapshotMetadataOffset));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                handle.Dispose();
                buffer.Dispose();
                votedFor = null;
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Indicates that <see cref="Options.IntegrityCheck"/> enabled and
    /// the internal state of the WAL didn't pass the integrity check.
    /// </summary>
    public sealed class InternalStateBrokenException : IntegrityException
    {
        internal InternalStateBrokenException()
            : base(ExceptionMessages.PersistentStateBroken)
        {
        }
    }

    private readonly NodeState state;

    /// <summary>
    /// Gets the index of the last committed log entry.
    /// </summary>
    public long LastCommittedEntryIndex
    {
        get => state.CommitIndex;
        private protected set => state.CommitIndex = value;
    }

    /// <summary>
    /// Gets the index of the last uncommitted log entry.
    /// </summary>
    public long LastUncommittedEntryIndex
    {
        get => state.LastIndex;
        private protected set => state.LastIndex = value;
    }

    private protected long LastAppliedEntryIndex
    {
        get => state.LastApplied;
        set => state.LastApplied = value;
    }

    private protected abstract long LastTerm { get; }

    private protected enum InternalStateScope : byte
    {
        Indexes = 0,
        Snapshot = 1,
        IndexesAndSnapshot = 2,
    }

    private protected ValueTask PersistInternalStateAsync(InternalStateScope scope) => scope switch
    {
        InternalStateScope.Indexes => state.FlushAsync(in NodeState.IndexesRange),
        InternalStateScope.Snapshot => state.FlushAsync(in NodeState.SnapshotRange),
        _ => state.FlushAsync(in NodeState.IndexesAndSnapshotRange),
    };
}