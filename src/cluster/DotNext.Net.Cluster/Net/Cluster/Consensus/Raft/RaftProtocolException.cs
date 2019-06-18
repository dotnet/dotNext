using System;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents violation of Raft protocol.
    /// </summary>
    [Serializable]
    public class RaftProtocolException : ConsensusProtocolException
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        /// <param name="message">Human-readable text describing problem.</param>
        public RaftProtocolException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="info">The serialized information about object.</param>
        /// <param name="context">The deserialization context.</param>
        protected RaftProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
