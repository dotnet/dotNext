using System;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct ControlOctet
    {
        private const int MessageTypeMask = 0B_0000_1111;
        private const int FlowControlMask = 0B_1111_0000;
        private readonly byte value;

        internal ControlOctet(MessageType type, FlowControl control)
            => value = (byte)((int)type | (int)control);

        internal ControlOctet(ReadOnlyMemory<byte> input, out int consumedBytes)
        {
            value = input.Span[0];
            consumedBytes = sizeof(byte);
        }

        internal void WriteTo(ref Memory<byte> output)
        {
            output.Span[0] = value;
            output = output.Slice(sizeof(byte));
        }

        internal MessageType Type => (MessageType)(value & MessageTypeMask);

        internal FlowControl Control => (FlowControl)(value & FlowControlMask);

        public static implicit operator byte(ControlOctet value) => value.value;
    }
}