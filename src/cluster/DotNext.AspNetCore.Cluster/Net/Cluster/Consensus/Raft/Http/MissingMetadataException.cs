namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class MissingMetadataException : RaftProtocolException
    {
        public MissingMetadataException()
            : base(ExceptionMessages.MissingMetadata)
        {
        }
    }
}
