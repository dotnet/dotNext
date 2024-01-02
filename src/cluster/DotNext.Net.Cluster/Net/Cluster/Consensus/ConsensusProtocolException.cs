using System.Net;

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
}