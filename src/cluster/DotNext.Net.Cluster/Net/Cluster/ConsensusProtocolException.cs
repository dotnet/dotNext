using System;
using System.Net;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents violation of network consensus protocol.
    /// </summary>
    [Serializable]
    public abstract class ConsensusProtocolException : ProtocolViolationException
    {
        protected ConsensusProtocolException(string message)
            : base(message)
        {
        }

        protected ConsensusProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}