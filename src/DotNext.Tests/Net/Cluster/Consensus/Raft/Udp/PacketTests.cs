using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    [ExcludeFromCodeCoverage]
    public sealed class PacketTests : Test
    {
        [Fact(Skip = "Attempt to find root cause of CLR internal error")]
        public static void Combination()
        {
            foreach(var type in Enum<MessageType>.Members)
                foreach(var control in Enum<FlowControl>.Members)
                {
                    var octet = new ControlOctet(type.Value, control.Value);
                    Equal(type.Value, octet.Type);
                    Equal(control.Value, octet.Control);
                }
        }

        [Fact(Skip = "Attempt to find root cause of CLR internal error")]
        public static void HeadersSerializationDeserialization()
        {
            Memory<byte> buffer = new byte[PacketHeaders.NaturalSize];
            foreach(var type in Enum<MessageType>.Members)
                foreach(var control in Enum<FlowControl>.Members)
                {
                    var headers = new PacketHeaders(type.Value, control.Value);
                    headers.WriteTo(buffer);
                    ReadOnlyMemory<byte> readOnlyView = buffer;
                    headers = new PacketHeaders(ref readOnlyView);
                    Equal(type.Value, headers.Type);
                    Equal(control.Value, headers.Control);
                }
        }
    }
}