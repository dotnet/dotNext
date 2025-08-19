using System.Buffers;

namespace DotNext.Net.Multiplexing;

internal abstract class ProtocolCommand
{
    public abstract void Write(IBufferWriter<byte> buffer);
}

internal sealed class StreamRejectedCommand(uint streamId) : ProtocolCommand
{
    public override void Write(IBufferWriter<byte> buffer) => Protocol.WriteStreamRejected(buffer, streamId);
}