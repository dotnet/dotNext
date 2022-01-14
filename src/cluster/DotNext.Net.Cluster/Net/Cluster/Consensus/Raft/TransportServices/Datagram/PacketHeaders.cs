namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

#pragma warning disable CA2252  // TODO: Remove in .NET 7
internal readonly struct PacketHeaders : IBinaryFormattable<PacketHeaders>
{
    /// <summary>
    /// Represents natural size without padding.
    /// </summary>
    internal const int Size = ControlOctet.Size;

    private readonly ControlOctet control;

    internal PacketHeaders(MessageType type, FlowControl control)
        => this.control = new ControlOctet(type, control);

    internal PacketHeaders(ref SpanReader<byte> input)
        => control = new ControlOctet(ref input);

    static PacketHeaders IBinaryFormattable<PacketHeaders>.Parse(ref SpanReader<byte> input) => new(ref input);

    static int IBinaryFormattable<PacketHeaders>.Size => Size;

    public void Format(ref SpanWriter<byte> output) => control.Format(ref output);

    internal MessageType Type => control.Type;

    internal FlowControl Control => control.Control;
}
#pragma warning restore CA2252