using System.Buffers;

namespace DotNext.Net.Multiplexing;

internal abstract class ProtocolCommand
{
    public abstract void Write(IBufferWriter<byte> buffer);
}

internal sealed class StreamRejectedCommand(ulong streamId) : ProtocolCommand
{
    public override void Write(IBufferWriter<byte> buffer) => Protocol.WriteStreamRejected(buffer, streamId);
}