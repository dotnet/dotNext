using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
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
            Memory<byte> buffer = new byte[PacketHeaders.NaturalSize];
            foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
                foreach (FlowControl control in Enum.GetValues(typeof(FlowControl)))
                {
                    var headers = new PacketHeaders(type, control);
                    headers.WriteTo(buffer);
                    ReadOnlyMemory<byte> readOnlyView = buffer;
                    headers = new PacketHeaders(readOnlyView, out _);
                    Equal(type, headers.Type);
                    Equal(control, headers.Control);
                }
        }
    }
}