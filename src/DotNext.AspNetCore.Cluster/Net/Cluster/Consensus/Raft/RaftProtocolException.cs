using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftProtocolException : Exception
    {
        internal RaftProtocolException(string message)
            : base(message)
        {
        }
    }
}
