using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// This is ephemeral state indicating that
    /// the cluster member will not become a leader.
    /// </summary>
    internal sealed class StandbyState : RaftState
    {
        internal StandbyState(IRaftStateMachine stateMachine)
            : base(stateMachine)
        {
        }

        internal override Task StopAsync() => Task.CompletedTask;
    }
}