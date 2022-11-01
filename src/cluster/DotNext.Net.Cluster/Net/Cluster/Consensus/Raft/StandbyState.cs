namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// This is ephemeral state indicating that
/// the cluster member will not become a leader.
/// </summary>
internal sealed class StandbyState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    internal StandbyState(IRaftStateMachine<TMember> stateMachine)
        : base(stateMachine)
    {
    }
}