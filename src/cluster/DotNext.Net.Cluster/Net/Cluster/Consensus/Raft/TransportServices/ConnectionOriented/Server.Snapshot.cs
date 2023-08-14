namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using IO.Log;

internal partial class Server
{
    private sealed class ReceivedSnapshot : StreamTransferObject, IRaftLogEntry
    {
        internal readonly ClusterMemberId Id;
        internal readonly long Term, Index;
        private readonly LogEntryMetadata metadata;

        internal ReceivedSnapshot(ProtocolStream stream)
            : base(stream, leaveOpen: true)
        {
            var reader = new SpanReader<byte>(stream.WrittenBufferSpan);
            (Id, Term, Index, metadata) = SnapshotMessage.Read(ref reader);
        }

        DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

        long? IDataTransferObject.Length => metadata.Length;

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        long IRaftLogEntry.Term => metadata.Term;

        public override bool IsReusable => false;

        bool ILogEntry.IsSnapshot => metadata.IsSnapshot;
    }
}