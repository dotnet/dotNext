using DotNext.Net.Cluster.Consensus.Raft.Membership;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    /// <inheritdoc/>
    public IClusterConfigurationStorage? ConfigurationStorage { get; set; }
}