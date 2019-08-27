using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal abstract class RaftState : Disposable
    {
        private protected readonly IRaftStateMachine stateMachine;

        private protected RaftState(IRaftStateMachine stateMachine) => this.stateMachine = stateMachine;

        internal abstract Task StopAsync();
    }
}
