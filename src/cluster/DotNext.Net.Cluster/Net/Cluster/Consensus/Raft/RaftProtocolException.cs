namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents violation of Raft protocol.
/// </summary>
/// <param name="message">Human-readable text describing problem.</param>
[Serializable]
public class RaftProtocolException(string message) : ConsensusProtocolException(message)
{
}