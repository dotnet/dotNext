using System;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal readonly struct PacketHeaders
    {
        /// <summary>
        /// Represents natural size without padding.
        /// </summary>
        internal const int NaturalSize = sizeof(byte);

        private readonly ControlOctet control;

        internal PacketHeaders(MessageType type, FlowControl control)
            => this.control = new ControlOctet(type, control);

        internal PacketHeaders(ReadOnlyMemory<byte> header, out int consumedBytes)
            => control = new ControlOctet(header, out consumedBytes);

        internal void WriteTo(Memory<byte> output)
        {
            control.WriteTo(ref output);
        }

        internal MessageType Type => control.Type;

        internal FlowControl Control => control.Control;
    }
}