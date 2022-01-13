namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using IO.Log;

internal partial class ProtocolStream
{
    private sealed class Snapshot : StreamTransferObject, IRaftLogEntry
    {
        private readonly LogEntryMetadata metadata;

        internal Snapshot(ProtocolStream stream, in LogEntryMetadata metadata)
            : base(stream, leaveOpen: true)
            => this.metadata = metadata;

        DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

        long? IDataTransferObject.Length => metadata.Length;

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        long IRaftLogEntry.Term => metadata.Term;

        public override bool IsReusable => false;

        bool ILogEntry.IsSnapshot => metadata.IsSnapshot;
    }
}