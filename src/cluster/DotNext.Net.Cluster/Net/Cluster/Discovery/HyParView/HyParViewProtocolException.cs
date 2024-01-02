namespace DotNext.Net.Cluster.Discovery.HyParView;

/// <summary>
/// Represents violation of Raft protocol.
/// </summary>
[Serializable]
public class HyParViewProtocolException : DiscoveryProtocolException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    /// <param name="message">Human-readable text describing problem.</param>
    public HyParViewProtocolException(string message)
        : base(message)
    {
    }
}