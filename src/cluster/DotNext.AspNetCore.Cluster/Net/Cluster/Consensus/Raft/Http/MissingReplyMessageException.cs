namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class MissingReplyMessageException : RaftProtocolException
    {
        public MissingReplyMessageException()
            : base(ExceptionMessages.MissingReply)
        {
        }
    }
}
