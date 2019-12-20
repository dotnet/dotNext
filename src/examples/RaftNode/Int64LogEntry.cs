using DotNext;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Replication;
using System;
using System.Buffers.Binary;

namespace RaftNode
{
    internal sealed class Int64LogEntry : BinaryTransferObject, IRaftLogEntry
    {
        internal Int64LogEntry(long value)
            : base(ToMemory(value))
        {
            Timestamp = DateTimeOffset.UtcNow;
        }

        bool ILogEntry.IsSnapshot => false;

        public long Term { get; set; }

        public DateTimeOffset Timestamp { get; }

        private static ReadOnlyMemory<byte> ToMemory(long value)
        {
            var result = new Memory<byte>(new byte[sizeof(long)]);
            BinaryPrimitives.WriteInt64LittleEndian(result.Span, value);
            return result;
        }
    }
}
