namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IDataTransferObject = IO.IDataTransferObject;

    internal interface IWriteOnlyLogEntry : IRaftLogEntry
    {
        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => null;
    }
}