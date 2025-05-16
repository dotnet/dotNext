using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Threading;
using IO.Log;
using BoxedClusterMemberId = Runtime.BoxedValue<ClusterMemberId>;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly AsyncExclusiveLock stateLock;
    private NodeState state;
    
    [StructLayout(LayoutKind.Auto)]
    private struct NodeState : IDisposable
    {
        private const string FileName = "state";

        private const int LastVotePresenceOffset = 0;
        private const int LastVoteOffset = LastVotePresenceOffset + sizeof(byte);
        private static readonly int TermOffset = LastVoteOffset + ClusterMemberId.Size;
        private static readonly int Size = TermOffset + sizeof(long);
        
        private readonly SafeFileHandle handle;
        private readonly byte[] buffer;
        private volatile BoxedClusterMemberId? votedFor;
        private long term; // volatile

        public NodeState(DirectoryInfo location)
        {
            var path = Path.Combine(location.FullName, FileName);
            long preallocationSize;
            FileMode mode;

            if (File.Exists(path))
            {
                preallocationSize = 0L;
                mode = FileMode.Open;
            }
            else
            {
                preallocationSize = Size;
                mode = FileMode.CreateNew;
            }

            handle = File.OpenHandle(path, mode, FileAccess.ReadWrite, FileShare.Read, FileOptions.WriteThrough, preallocationSize);
            buffer = GC.AllocateUninitializedArray<byte>(Size, pinned: true);

            if (RandomAccess.Read(handle, buffer, fileOffset: 0L) < buffer.Length)
            {
                Array.Clear(buffer);
                RandomAccess.Write(handle, buffer, fileOffset: 0L);
            }

            if (Unsafe.BitCast<byte, bool>(buffer[LastVotePresenceOffset]))
                votedFor = BoxedClusterMemberId.Box(new ClusterMemberId(buffer.AsSpan(LastVoteOffset)));
            term = ReadInt64LittleEndian(buffer.AsSpan(TermOffset));
        }

        public readonly long Term => Volatile.Read(in term);
        
        public readonly bool IsVotedFor(in ClusterMemberId expected) => IPersistentState.IsVotedFor(votedFor, expected);
        
        public void UpdateTerm(long value, bool resetLastVote)
        {
            WriteInt64LittleEndian(buffer.AsSpan(TermOffset), value);
            if (resetLastVote)
            {
                votedFor = null;
                buffer[LastVotePresenceOffset] = Unsafe.BitCast<bool, byte>(false);
            }

            Volatile.Write(ref term, value);
        }
        
        public long IncrementTerm(ClusterMemberId id)
        {
            var result = Interlocked.Increment(ref term);
            WriteInt64LittleEndian(buffer.AsSpan(TermOffset), result);
            UpdateVotedFor(id);
            return result;
        }
        
        public void UpdateVotedFor(ClusterMemberId id)
        {
            votedFor = BoxedClusterMemberId.Box(id);
            buffer[LastVotePresenceOffset] = Unsafe.BitCast<bool, byte>(true);
            id.Format(buffer.AsSpan(LastVoteOffset));
        }

        public readonly ValueTask FlushAsync(CancellationToken token = default)
            => RandomAccess.WriteAsync(handle, buffer, fileOffset: 0L, token);

        public void Dispose()
        {
            handle?.Dispose();
            this = default;
        }
    }
    
    /// <summary>
    /// Indicates that the node state data is broken on the disk.
    /// </summary>
    public sealed class InternalStateBrokenException : IntegrityException
    {
        internal InternalStateBrokenException()
            : base(ExceptionMessages.PersistentStateBroken)
        {
        }
    }

    /// <inheritdoc/>
    bool IPersistentState.IsVotedFor(in ClusterMemberId id) => state.IsVotedFor(in id);

    /// <inheritdoc/>
    long IPersistentState.Term => state.Term;

    /// <inheritdoc/>
    async ValueTask<long> IPersistentState.IncrementTermAsync(ClusterMemberId member, CancellationToken token)
    {
        await stateLock.AcquireAsync(token).ConfigureAwait(false);
        long term;
        try
        {
            term = state.IncrementTerm(member);
            await state.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            stateLock.Release();
        }

        return term;
    }

    /// <inheritdoc/>
    async ValueTask IPersistentState.UpdateTermAsync(long term, bool resetLastVote, CancellationToken token)
    {
        await stateLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            state.UpdateTerm(term, resetLastVote);
            await state.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            stateLock.Release();
        }
    }

    /// <inheritdoc/>
    async ValueTask IPersistentState.UpdateVotedForAsync(ClusterMemberId member, CancellationToken token)
    {
        await stateLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            state.UpdateVotedFor(member);
            await state.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            stateLock.Release();
        }
    }
}