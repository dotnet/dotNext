namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;

partial class Server
{
    private class ProtocolStreamSegment(ProtocolStream protocol) : IDataTransferObject
    {
        private ProtocolStream? protocol = protocol;

        public ValueTask EnsureConsumedAsync(CancellationToken token)
        {
            ValueTask task;
            if (protocol is null)
            {
                task = ValueTask.CompletedTask;
            }
            else
            {
                task = protocol.SkipAsync(token);
                protocol = null;
            }

            return task;
        }
        
        bool IDataTransferObject.IsReusable => false;

        long? IDataTransferObject.Length => null;
        
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask task;
            if (protocol is null)
            {
                task = ValueTask.CompletedTask;
            }
            else
            {
                task = writer.CopyFromAsync(protocol, count: null, token);
                protocol = null;
            }

            return task;
        }
    }
}