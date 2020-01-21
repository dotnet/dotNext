using System;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using static Threading.AtomicInt64;

    internal sealed class DuplicationControlContext
    {
        private Guid clientVersion;
        private long sequenceNumber;

        //for local node only
        internal void Initialize() => clientVersion = Guid.NewGuid();
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool IsDuplicated(in Guid clientVersion, long sequenceNumber)
        {
            if(BitwiseComparer<Guid>.Equals(in clientVersion, in this.clientVersion) && this.sequenceNumber <= sequenceNumber)
                return true;
            this.clientVersion = clientVersion;
            this.sequenceNumber = sequenceNumber;
            return false;
        }

        //for incoming message
        internal bool IsDuplicated(HttpMessage message) => IsDuplicated(in message.NodeVersion, message.SequenceNumber);

        //for outcoming message
        internal void GetControlData(out Guid version, out long sequenceNumber)
        {
            sequenceNumber = this.sequenceNumber.IncrementAndGet();
            version = clientVersion;
        }
    }
}