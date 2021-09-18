using System.Net;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Consensus;

/// <summary>
/// Represents violation of network consensus protocol.
/// </summary>
[Serializable]
public abstract class ConsensusProtocolException : ProtocolViolationException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    /// <param name="message">Human-readable text describing problem.</param>
    protected ConsensusProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Deserialization constructor.
    /// </summary>
    /// <param name="info">The serialized information about object.</param>
    /// <param name="context">The deserialization context.</param>
    protected ConsensusProtocolException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}