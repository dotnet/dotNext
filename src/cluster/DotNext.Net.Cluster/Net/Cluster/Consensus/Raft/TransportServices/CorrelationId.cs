using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;

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

        internal CorrelationId(ReadOnlySpan<byte> bytes, out int consumedBytes)
        {
            var reader = new SpanReader<byte>(bytes);

            ApplicationId = reader.ReadInt64(true);
            StreamId = reader.ReadInt64(true);

            consumedBytes = reader.ConsumedCount;
        }

        internal void WriteTo(Span<byte> output)
        {
            var writer = new SpanWriter<byte>(output);

            writer.WriteInt64(ApplicationId, true);
            writer.WriteInt64(StreamId, true);
        }

        private bool Equals(in CorrelationId other)
            => ApplicationId == other.ApplicationId && StreamId == other.StreamId;

        public bool Equals(CorrelationId other) => Equals(in other);

        public override bool Equals([NotNullWhen(true)] object? other) => other is CorrelationId id && Equals(in id);

        public override int GetHashCode() => HashCode.Combine(ApplicationId, StreamId);

        public override string ToString() => $"App Id={ApplicationId:X}, Stream Id={StreamId:X}";

        public static bool operator ==(in CorrelationId x, in CorrelationId y)
            => x.Equals(in y);

        public static bool operator !=(in CorrelationId x, in CorrelationId y)
            => !x.Equals(in y);
    }
}