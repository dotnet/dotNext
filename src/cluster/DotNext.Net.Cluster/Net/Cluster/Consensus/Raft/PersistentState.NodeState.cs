using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
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

            private const byte True = 1;
            private const byte False = 0;
            private const string FileName = "node.state";
            private const long Capacity = 128;
            private const long TermOffset = 0L;
            private const long CommitIndexOffset = TermOffset + sizeof(long);
            private const long LastAppliedOffset = CommitIndexOffset + sizeof(long);
            private const long LastIndexOffset = LastAppliedOffset + sizeof(long);
            private const long LastVotePresenceOffset = LastIndexOffset + sizeof(long);
            private const long LastVoteOffset = LastVotePresenceOffset + sizeof(byte);

            private readonly MemoryMappedFile mappedFile;
            private readonly MemoryMappedViewAccessor stateView;
            private readonly IWriteLock syncRoot;

            // boxed ClusterMemberId or null if there is not last vote stored
            private volatile object? votedFor;
            private long term, commitIndex, lastIndex, lastApplied;  // volatile

            internal NodeState(DirectoryInfo location, IWriteLock writeLock)
            {
                mappedFile = MemoryMappedFile.CreateFromFile(Path.Combine(location.FullName, FileName), FileMode.OpenOrCreate, null, Capacity, MemoryMappedFileAccess.ReadWrite);
                syncRoot = writeLock;
                stateView = mappedFile.CreateViewAccessor();
                term = stateView.ReadInt64(TermOffset);
                commitIndex = stateView.ReadInt64(CommitIndexOffset);
                lastIndex = stateView.ReadInt64(LastIndexOffset);
                lastApplied = stateView.ReadInt64(LastAppliedOffset);
                var hasLastVote = ValueTypeExtensions.ToBoolean(stateView.ReadByte(LastVotePresenceOffset));
                if (hasLastVote)
                {
                    stateView.Read(LastVoteOffset, out ClusterMemberId votedFor);
                    this.votedFor = votedFor;
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

            private static bool IsCommitted(NodeState state, long index) => index <= state.commitIndex.VolatileRead();

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

            internal async ValueTask UpdateTermAsync(long value, bool resetLastVote)
            {
                await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    stateView.Write(TermOffset, value);
                    if (resetLastVote)
                    {
                        stateView.Write(LastVotePresenceOffset, False);
                        votedFor = null;
                    }

                    stateView.Flush();
                    term.VolatileWrite(value);
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            internal async ValueTask<long> IncrementTermAsync()
            {
                await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    var result = term.IncrementAndGet();
                    stateView.Write(TermOffset, result);
                    stateView.Flush();
                    return result;
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            internal bool IsVotedFor(ClusterMemberId? expected)
            {
                var actual = votedFor;

                // avoid placing value type on the stack
                return actual is null || (expected.HasValue && Unsafe.Unbox<ClusterMemberId>(actual).Equals(expected.GetValueOrDefault()));
            }

            private async ValueTask UpdateVotedForAsync(ClusterMemberId member)
            {
                await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    stateView.Write(LastVotePresenceOffset, True);
                    stateView.Write(LastVoteOffset, ref member);
                    votedFor = member;
                    stateView.Flush();
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            private async ValueTask UpdateVotedForAsync()
            {
                await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    stateView.Write(LastVotePresenceOffset, False);
                    votedFor = null;
                    stateView.Flush();
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            internal ValueTask UpdateVotedForAsync(ClusterMemberId? member)
                => member.HasValue ? UpdateVotedForAsync(member.GetValueOrDefault()) : UpdateVotedForAsync();

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

        private readonly NodeState state;
        private long lastTerm;  // term of last committed entry

        /// <summary>
        /// Gets index of the committed or last log entry.
        /// </summary>
        /// <remarks>
        /// This method is synchronous because returning value should be cached and updated in memory by implementing class.
        /// </remarks>
        /// <param name="committed"><see langword="true"/> to get the index of highest log entry known to be committed; <see langword="false"/> to get the index of the last log entry.</param>
        /// <returns>The index of the log entry.</returns>
        public long GetLastIndex(bool committed) => committed ? state.CommitIndex : state.LastIndex;
    }
}
