namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents catastrophic WAL failure.
    /// </summary>
    public abstract class IntegrityException : Exception
    {
        private protected IntegrityException(string message)
            : base(message)
        {
        }
    }
}