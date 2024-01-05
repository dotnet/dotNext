using System.Net;

namespace DotNext.Net.Cluster.Discovery;

/// <summary>
/// Represents violation of peer discovery protocol.
/// </summary>
/// <param name="message">Human-readable text describing problem.</param>
[Serializable]
public abstract class DiscoveryProtocolException(string message) : ProtocolViolationException(message)
{
}