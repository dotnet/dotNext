namespace DotNext.Net.Cluster.Discovery.HyParView;

/// <summary>
/// Represents violation of Raft protocol.
/// </summary>
/// <param name="message">Human-readable text describing problem.</param>
[Serializable]
public class HyParViewProtocolException(string message) : DiscoveryProtocolException(message)
{
}