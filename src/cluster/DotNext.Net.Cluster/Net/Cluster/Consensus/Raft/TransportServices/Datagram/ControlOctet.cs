using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

[StructLayout(LayoutKind.Auto)]
internal readonly struct ControlOctet : IBinaryFormattable<ControlOctet>
{
    internal const int Size = sizeof(byte);
    private const int MessageTypeMask = 0B_0000_1111;
    private const int FlowControlMask = 0B_1111_0000;
    private readonly byte value;

    internal ControlOctet(MessageType type, FlowControl control)
        => value = (byte)((int)type | (int)control);

    internal ControlOctet(ref SpanReader<byte> input)
        => value = input.Read();

    static ControlOctet IBinaryFormattable<ControlOctet>.Parse(ref SpanReader<byte> input) => new(ref input);

    static int IBinaryFormattable<ControlOctet>.Size => Size;

    public void Format(ref SpanWriter<byte> output) => output.Write(value);

    internal MessageType Type => (MessageType)(value & MessageTypeMask);

    internal FlowControl Control => (FlowControl)(value & FlowControlMask);

    public static implicit operator byte(ControlOctet value) => value.value;
}