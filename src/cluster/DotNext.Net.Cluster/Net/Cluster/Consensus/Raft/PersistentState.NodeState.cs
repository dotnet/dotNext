﻿using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.IO.Hashing;
using static System.Buffers.Binary.BinaryPrimitives;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using BoxedClusterMemberId = Runtime.BoxedValue<ClusterMemberId>;
using IntegrityException = IO.Log.IntegrityException;

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

        // boxed ClusterMemberId or null if there is no last vote stored
        private volatile BoxedClusterMemberId? votedFor;
        private long term, commitIndex, lastIndex, lastApplied;  // volatile
        private SnapshotMetadata snapshot; // cached snapshot metadata to avoid backward writes

        private NodeState(string fileName, MemoryAllocator<byte> allocator, bool integrityCheck, bool writeThrough)
        {
            Debug.Assert(Capacity >= LastVoteOffset + sizeof(long));

            buffer = allocator.AllocateExactly(Capacity);
            if (File.Exists(fileName))
            {
                handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
                RandomAccess.Read(handle, buffer.Span, 0L);
            }
            else
            {
                handle = File.OpenHandle(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.WriteThrough, Capacity);
                buffer.Span.Clear();

                FileAttributes attributes;
                if (integrityCheck)
                {
                    attributes = FileAttributes.NotContentIndexed | FileAttributes.IntegrityStream;
                    WriteInt64LittleEndian(Checksum, FNV1a64.Hash(Data));
                }
                else
                {
                    attributes = FileAttributes.NotContentIndexed;
                }

                File.SetAttributes(handle, attributes);
                RandomAccess.Write(handle, buffer.Span, 0L);
            }

            // reopen handle in asynchronous mode
            handle.Dispose();

            // FileOptions.RandomAccess to keep the whole file cached in the page cache
            const FileOptions dontSkipBuffer = FileOptions.Asynchronous | FileOptions.RandomAccess;
            const FileOptions skipBuffer = FileOptions.Asynchronous | FileOptions.WriteThrough | FileOptions.RandomAccess;
            handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, writeThrough ? skipBuffer : dontSkipBuffer);

            // restore state
            ReadOnlySpan<byte> bufferSpan = buffer.Span;
            term = ReadInt64LittleEndian(bufferSpan.Slice(TermOffset));
            commitIndex = ReadInt64LittleEndian(bufferSpan.Slice(CommitIndexOffset));
            lastIndex = ReadInt64LittleEndian(bufferSpan.Slice(LastIndexOffset));
            lastApplied = ReadInt64LittleEndian(bufferSpan.Slice(LastAppliedOffset));
            snapshot = new(bufferSpan.Slice(SnapshotMetadataOffset));
            if (Unsafe.BitCast<byte, bool>(bufferSpan[LastVotePresenceOffset]))
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
            => !integrityCheck || FNV1a64.Hash(Data) == ReadInt64LittleEndian(Checksum);

        internal ValueTask FlushAsync(in Range range, CancellationToken token = default)
        {
            ReadOnlyMemory<byte> data = buffer.Memory;
            int offset;

            if (integrityCheck)
            {
                WriteInt64LittleEndian(Checksum, FNV1a64.Hash(Data));
                offset = 0;
            }
            else
            {
                (offset, var length) = range.GetOffsetAndLength(data.Length);
                data = data.Slice(offset, length);
            }

            return RandomAccess.WriteAsync(handle, data, offset, token);
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
            get => Volatile.Read(in commitIndex);
            set
            {
                WriteInt64LittleEndian(buffer.Span.Slice(CommitIndexOffset), value);
                Volatile.Write(ref commitIndex, value);
            }
        }

        internal long LastApplied
        {
            get => Volatile.Read(in lastApplied);
            set
            {
                WriteInt64LittleEndian(buffer.Span.Slice(LastAppliedOffset), value);
                Volatile.Write(ref lastApplied, value);
            }
        }

        internal long LastIndex
        {
            get => Volatile.Read(in lastIndex);
            set
            {
                WriteInt64LittleEndian(buffer.Span.Slice(LastIndexOffset), value);
                Volatile.Write(ref lastIndex, value);
            }
        }

        internal long TailIndex => LastIndex + 1L;

        internal long Term => Volatile.Read(in term);

        internal void UpdateTerm(long value, bool resetLastVote)
        {
            WriteInt64LittleEndian(buffer.Span.Slice(TermOffset), value);
            if (resetLastVote)
            {
                votedFor = null;
                buffer[LastVotePresenceOffset] = False;
            }

            Volatile.Write(ref term, value);
        }

        internal long IncrementTerm(ClusterMemberId id)
        {
            var result = Interlocked.Increment(ref term);
            WriteInt64LittleEndian(buffer.Span.Slice(TermOffset), result);
            UpdateVotedFor(id);
            return result;
        }

        internal bool IsVotedFor(in ClusterMemberId expected) => IPersistentState.IsVotedFor(votedFor, expected);

        internal void UpdateVotedFor(ClusterMemberId id)
        {
            votedFor = BoxedClusterMemberId.Box(id);
            buffer[LastVotePresenceOffset] = True;
            id.Format(buffer.Span.Slice(LastVoteOffset));
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

    [StructLayout(LayoutKind.Auto)]
    private readonly struct CommitChecker : ISupplier<bool>
    {
        private readonly NodeState state;
        private readonly long index;

        internal CommitChecker(NodeState state, long index)
        {
            Debug.Assert(state is not null);

            this.state = state;
            this.index = index;
        }

        bool ISupplier<bool>.Invoke() => index <= state.CommitIndex;
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
    /// Gets the index of the last added log entry.
    /// </summary>
    public long LastEntryIndex
    {
        get => state.LastIndex;
        private protected set => state.LastIndex = value;
    }

    private protected long LastAppliedEntryIndex
    {
        get => state.LastApplied;
        set => state.LastApplied = value;
    }

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