using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
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
            4 bytes = Node port
            4 bytes = Address Length
            octet string = IP Address (16 bytes for IPv6)
         */
        private sealed class NodeState : Disposable
        {
            internal static readonly Func<NodeState, long, bool> IsCommittedPredicate = IsCommitted;

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
            private volatile IPEndPoint? votedFor;
            private long term, commitIndex, lastIndex, lastApplied;  // volatile

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
                {
                    votedFor = null;
                }
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

            internal async ValueTask UpdateTermAsync(long value)
            {
                using (await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    stateView.Write(TermOffset, value);
                    stateView.Flush();
                    term.VolatileWrite(value);
                }
            }

            internal async ValueTask<long> IncrementTermAsync()
            {
                using (await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    var result = term.IncrementAndGet();
                    stateView.Write(TermOffset, result);
                    stateView.Flush();
                    return result;
                }
            }

            internal bool IsVotedFor(IPEndPoint? member)
            {
                var lastVote = votedFor;
                return lastVote is null || Equals(lastVote, member);
            }

            internal async ValueTask UpdateVotedForAsync(IPEndPoint? member)
            {
                using (await syncRoot.AcquireAsync(CancellationToken.None).ConfigureAwait(false))
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
