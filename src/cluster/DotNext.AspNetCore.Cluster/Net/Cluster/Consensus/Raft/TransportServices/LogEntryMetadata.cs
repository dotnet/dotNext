using System;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;

    /// <summary>
    /// Represents serializable log entry metadata that
    /// can be passed over the wire using HTTP protocol.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct LogEntryMetadata
    {
        internal const int Size = sizeof(long) + sizeof(byte) + sizeof(int) + sizeof(long) + sizeof(long);
        private const byte NoFlags = 0;
        private const byte HasIdentifierFlag = 0x01;

        private readonly long timestamp;
        private readonly byte flags;
        private readonly int identifier;

        private LogEntryMetadata(long term, DateTimeOffset timestamp, long length, int? identifier)
        {
            Term = term;
            this.timestamp = timestamp.UtcTicks;
            Length = length;
            flags = identifier.HasValue ? HasIdentifierFlag : NoFlags;
            this.identifier = identifier.GetValueOrDefault();
        }

        internal static LogEntryMetadata Create<TEntry>(TEntry entry)
            where TEntry : notnull, IRaftLogEntry
            => new LogEntryMetadata(entry.Term, entry.Timestamp, entry.Length.GetValueOrDefault(), entry.CommandId);

        internal LogEntryMetadata(ReadOnlySpan<byte> input)
        {
            var reader = new SpanReader<byte>(input);
            Term = reader.ReadInt64(true);
            timestamp = reader.ReadInt64(true);
            flags = reader.Read();
            identifier = reader.ReadInt32(true);
            Length = reader.ReadInt64(true);
        }

        internal long Term { get; }

        internal long Length { get; }

        internal DateTimeOffset Timestamp => new DateTimeOffset(timestamp, TimeSpan.Zero);

        internal int? CommandId => (flags & HasIdentifierFlag) != 0 ? identifier : null;

        internal void Serialize(Span<byte> output)
        {
            var writer = new SpanWriter<byte>(output);
            writer.WriteInt64(Term, true);
            writer.WriteInt64(timestamp, true);
            writer.Add(flags);
            writer.WriteInt32(identifier, true);
            writer.WriteInt64(Length, true);
        }
    }
}