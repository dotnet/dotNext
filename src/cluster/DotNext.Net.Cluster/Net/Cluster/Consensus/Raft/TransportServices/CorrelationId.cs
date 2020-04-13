using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct CorrelationId : IEquatable<CorrelationId>
    {
        /// <summary>
        /// Represents the size of this struct without padding.
        /// </summary>
        internal const int NaturalSize = sizeof(long) + sizeof(long);

        /// <summary>
        /// The identifier generated randomly at client startup.
        /// </summary>
        internal readonly long ApplicationId;

        /// <summary>
        /// The identifier of the stream.
        /// </summary>
        internal readonly long StreamId;

        internal CorrelationId(long applicationId, long streamId)
        {
            ApplicationId = applicationId;
            StreamId = streamId;
        }

        internal CorrelationId(ref ReadOnlyMemory<byte> bytes)
        {
            ApplicationId = BinaryPrimitives.ReadInt64LittleEndian(bytes.Span);

            bytes = bytes.Slice(sizeof(long));
            StreamId = BinaryPrimitives.ReadInt64LittleEndian(bytes.Span);

            bytes = bytes.Slice(sizeof(long));
        }

        internal void WriteTo(Memory<byte> output)
        {
            BinaryPrimitives.WriteInt64LittleEndian(output.Span, ApplicationId);
            output = output.Slice(sizeof(long));

            BinaryPrimitives.WriteInt64LittleEndian(output.Span, StreamId);
        }

        public bool Equals(CorrelationId other)
            => ApplicationId == other.ApplicationId && StreamId == other.StreamId;

        public override bool Equals(object other) => other is CorrelationId id && Equals(id);

        public override int GetHashCode() => HashCode.Combine(ApplicationId, StreamId);

        public override string ToString() => $"App Id={ApplicationId:X}, Stream Id={StreamId:X}";

        public static bool operator ==(in CorrelationId x, in CorrelationId y)
            => x.ApplicationId == y.ApplicationId && x.StreamId == y.StreamId;

        public static bool operator !=(in CorrelationId x, in CorrelationId y)
            => x.ApplicationId != y.ApplicationId || x.StreamId != y.StreamId;
    }
}