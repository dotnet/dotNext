namespace DotNext.Net.Cluster.Consensus.Raft;

internal abstract class ConsensusState<TMember>(IRaftStateMachine<TMember> stateMachine) : RaftState<TMember>(stateMachine)
    where TMember : class, IRaftClusterMember
{
    public abstract CancellationToken Token { get; }
}