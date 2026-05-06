namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented;

using IO;
using IO.Log;

internal partial class Server
{
    private sealed class ReceivedSnapshot: ProtocolStreamSegment, IRaftLogEntry
    {
        internal readonly SnapshotMessage Message;

        public ReceivedSnapshot(ProtocolStream protocol)
            : base(protocol)
        {
            Message = SnapshotMessage.Parse(protocol.WrittenBufferSpan);
            protocol.AdvanceReadCursor(SnapshotMessage.Size);
        }

        long? IDataTransferObject.Length => Message.Metadata.Length;

        int? IRaftLogEntry.CommandId => Message.Metadata.CommandId;

        long IRaftLogEntry.Term => Message.Metadata.Term;

        bool ILogEntry.IsSnapshot => Message.Metadata.IsSnapshot;
    }
}