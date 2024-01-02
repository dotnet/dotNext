namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using IO.Log;

internal partial class Server
{
    private sealed class ReceivedSnapshot : StreamTransferObject, IRaftLogEntry
    {
        internal readonly SnapshotMessage Message;

        internal ReceivedSnapshot(ProtocolStream stream)
            : base(stream, leaveOpen: true)
            => Message = SnapshotMessage.Parse(stream.WrittenBufferSpan);

        DateTimeOffset ILogEntry.Timestamp => Message.Metadata.Timestamp;

        long? IDataTransferObject.Length => Message.Metadata.Length;

        int? IRaftLogEntry.CommandId => Message.Metadata.CommandId;

        long IRaftLogEntry.Term => Message.Metadata.Term;

        public override bool IsReusable => false;

        bool ILogEntry.IsSnapshot => Message.Metadata.IsSnapshot;
    }
}