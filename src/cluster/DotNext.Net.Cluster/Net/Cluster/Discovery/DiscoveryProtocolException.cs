using System.Net;

namespace DotNext.Net.Cluster.Discovery;

/// <summary>
/// Represents violation of peer discovery protocol.
/// </summary>
[Serializable]
public abstract class DiscoveryProtocolException : ProtocolViolationException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    /// <param name="message">Human-readable text describing problem.</param>
    protected DiscoveryProtocolException(string message)
        : base(message)
    {
    }
}