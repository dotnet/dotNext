using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftProtocolException : ConsensusProtocolException
    {
        internal RaftProtocolException(string message)
            : base(message)
        {
        }
    }
}
