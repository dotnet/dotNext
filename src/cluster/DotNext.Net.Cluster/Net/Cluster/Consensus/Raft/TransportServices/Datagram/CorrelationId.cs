using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

#pragma warning disable CA2252  // TODO: Remove in .NET 7
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CorrelationId : IEquatable<CorrelationId>, IBinaryFormattable<CorrelationId>
{
    /// <summary>
    /// Represents the size of this struct without padding.
    /// </summary>
    internal const int Size = sizeof(long) + sizeof(long);

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

    internal CorrelationId(ref SpanReader<byte> reader)
    {
        ApplicationId = reader.ReadInt64(true);
        StreamId = reader.ReadInt64(true);
    }

    static CorrelationId IBinaryFormattable<CorrelationId>.Parse(ref SpanReader<byte> input) => new(ref input);

    static int IBinaryFormattable<CorrelationId>.Size => Size;

    public void Format(ref SpanWriter<byte> output)
    {
        output.WriteInt64(ApplicationId, true);
        output.WriteInt64(StreamId, true);
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
#pragma warning restore CA2252