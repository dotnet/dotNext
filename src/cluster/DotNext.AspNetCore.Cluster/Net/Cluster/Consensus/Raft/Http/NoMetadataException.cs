namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class NoMetadataException : ConsensusProtocolException
    {
        public NoMetadataException()
            : base(ExceptionMessages.NoMetadataPresent)
        {
        }
    }
}
