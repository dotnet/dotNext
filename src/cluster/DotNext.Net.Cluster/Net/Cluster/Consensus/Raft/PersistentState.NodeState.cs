using static System.Buffers.Binary.BinaryPrimitives;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using Threading;

public partial class PersistentState
{
    /*
        State file format:
        8 bytes = Term
        8 bytes = CommitIndex
        8 bytes = LastApplied
        8 bytes = LastIndex
        1 byte = presence of cluster member id
        sizeof(ClusterMemberId) = last vote
     */
    private sealed class NodeState : Disposable
    {
        internal static readonly Func<NodeState, long, bool> IsCommittedPredicate = IsCommitted;

        private const string FileName = "node.state";
        private const byte False = 0;
        private const byte True = 1;
        private const int Capacity = 128;
        private const int TermOffset = 0;
        private const int CommitIndexOffset = TermOffset + sizeof(long);
        private const int LastAppliedOffset = CommitIndexOffset + sizeof(long);
        private const int LastIndexOffset = LastAppliedOffset + sizeof(long);
        private const int LastVotePresenceOffset = LastIndexOffset + sizeof(long);
        private const int LastVoteOffset = LastVotePresenceOffset + sizeof(byte);

        private readonly SafeFileHandle handle;
        private MemoryOwner<byte> buffer;

        // boxed ClusterMemberId or null if there is not last vote stored
        private volatile object? votedFor;
        private long term, commitIndex, lastIndex, lastApplied;  // volatile

        private NodeState(string fileName, MemoryAllocator<byte> allocator)
        {
            buffer = allocator.Invoke(Capacity, true);

            FileMode fileMode;
            long initialSize;
            if (File.Exists(fileName))
            {
                fileMode = FileMode.OpenOrCreate;
                initialSize = 0L;
            }
            else
            {
                fileMode = FileMode.CreateNew;
                initialSize = Capacity;
            }

            // open file in synchronous mode to restore the state
            handle = File.OpenHandle(fileName, fileMode, FileAccess.ReadWrite, FileShare.Read, FileOptions.None, initialSize);
            if (RandomAccess.Read(handle, buffer.Span, 0L) < Capacity)
            {
                buffer.Span.Clear();
                RandomAccess.Write(handle, buffer.Span, 0L);
            }

            // restore state
            ReadOnlySpan<byte> bufferSpan = buffer.Span;
            term = ReadInt64LittleEndian(bufferSpan.Slice(TermOffset));
            commitIndex = ReadInt64LittleEndian(bufferSpan.Slice(CommitIndexOffset));
            lastIndex = ReadInt64LittleEndian(bufferSpan.Slice(LastIndexOffset));
            lastApplied = ReadInt64LittleEndian(bufferSpan.Slice(LastAppliedOffset));
            var hasLastVote = ValueTypeExtensions.ToBoolean(bufferSpan[LastVotePresenceOffset]);
            if (hasLastVote)
                this.votedFor = new ClusterMemberId(bufferSpan.Slice(LastVoteOffset));

            // reopen handle in asynchronous mode
            handle.Dispose();
            handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous);
        }

        internal NodeState(DirectoryInfo location, MemoryAllocator<byte> allocator)
            : this(Path.Combine(location.FullName, FileName), allocator)
        {
        }

        internal ValueTask FlushAsync(CancellationToken token = default)
            => RandomAccess.WriteAsync(handle, buffer.Memory, 0L, token);

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

        internal ValueTask UpdateTermAsync(long value, bool resetLastVote, CancellationToken token = default)
        {
            WriteInt64LittleEndian(buffer.Span.Slice(TermOffset), value);
            if (resetLastVote)
            {
                votedFor = null;
                buffer[LastVotePresenceOffset] = False;
            }

            term.VolatileWrite(value);
            return FlushAsync(token);
        }

        internal async ValueTask<long> IncrementTermAsync()
        {
            var result = term.IncrementAndGet();
            WriteInt64LittleEndian(buffer.Span.Slice(TermOffset), result);
            await FlushAsync().ConfigureAwait(false);
            return result;
        }

        internal bool IsVotedFor(in ClusterMemberId? expected) => IPersistentState.IsVotedFor(votedFor, expected);

        internal ValueTask UpdateVotedForAsync(ClusterMemberId? member)
        {
            if (member.HasValue)
            {
                var id = member.GetValueOrDefault();
                votedFor = id;
                buffer[LastVotePresenceOffset] = True;
#pragma warning disable CA2252 // TODO: Remove in .NET 7
                IBinaryFormattable<ClusterMemberId>.Format(id, buffer.Span.Slice(LastVoteOffset));
#pragma warning restore CA2252
            }
            else
            {
                votedFor = null;
                buffer[LastVotePresenceOffset] = False;
            }

            return FlushAsync();
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

    private readonly NodeState state;
    private long lastTerm;  // term of last committed entry

    /// <summary>
    /// Gets the index of the last committed log entry.
    /// </summary>
    public long LastCommittedEntryIndex => state.CommitIndex;

    /// <summary>
    /// Gets the index of the last uncommitted log entry.
    /// </summary>
    public long LastUncommittedEntryIndex => state.LastIndex;

    /// <summary>
    /// Gets the index of the last committed log entry applied to underlying state machine.
    /// </summary>
    public long LastAppliedEntryIndex => state.LastApplied;
}