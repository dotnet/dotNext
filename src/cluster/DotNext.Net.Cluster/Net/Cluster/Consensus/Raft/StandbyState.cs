namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// This is ephemeral state indicating that
/// the cluster member will not become a leader.
/// </summary>
internal sealed class StandbyState<TMember>(IRaftStateMachine<TMember> stateMachine) : RaftState<TMember>(stateMachine)
    where TMember : class, IRaftClusterMember
{
    internal bool Resumable { get; init; } = true;
}