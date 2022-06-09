using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

[ExcludeFromCodeCoverage]
public sealed class PacketTests : Test
{
    [Fact]
    public static void Combination()
    {
        foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
            foreach (FlowControl control in Enum.GetValues(typeof(FlowControl)))
            {
                var octet = new ControlOctet(type, control);
                Equal(type, octet.Type);
                Equal(control, octet.Control);
            }
    }

    [Fact]
    public static void HeadersSerializationDeserialization()
    {
        Span<byte> buffer = new byte[PacketHeaders.Size];
        foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
            foreach (FlowControl control in Enum.GetValues(typeof(FlowControl)))
            {
                var headers = new PacketHeaders(type, control);
                IBinaryFormattable<PacketHeaders>.Format(headers, buffer);
                headers = IBinaryFormattable<PacketHeaders>.Parse(buffer);
                Equal(type, headers.Type);
                Equal(control, headers.Control);
            }
    }
}