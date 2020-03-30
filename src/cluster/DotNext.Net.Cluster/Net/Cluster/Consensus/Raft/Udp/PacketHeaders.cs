using System;
using System.Buffers.Binary;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal readonly struct PacketHeaders
    {
        /// <summary>
        /// Represents natural size without padding.
        /// </summary>
        internal const int NaturalSize = sizeof(long) + sizeof(byte);   

        private readonly ControlOctet control;
        internal readonly long Term;

        internal PacketHeaders(MessageType type, FlowControl control, long term)
        {
            this.control = new ControlOctet(type, control);
            Term = term;
        }

        internal PacketHeaders(ref ReadOnlyMemory<byte> header)
        {
            control = new ControlOctet(ref header);

            Term = BinaryPrimitives.ReadInt64LittleEndian(header.Span);
            header = header.Slice(sizeof(long));
        }

        internal void WriteTo(Memory<byte> output)
        {
            control.WriteTo(ref output);

            BinaryPrimitives.WriteInt64LittleEndian(output.Span, Term);
        }

        internal MessageType Type => control.Type;

        internal FlowControl Control => control.Control;
    }
}