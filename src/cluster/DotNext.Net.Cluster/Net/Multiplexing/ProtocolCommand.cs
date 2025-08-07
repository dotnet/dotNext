using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Multiplexing;

using Patterns;

internal abstract class ProtocolCommand
{
    public abstract int Write(Span<byte> buffer);
}

internal sealed class HeartbeatCommand : ProtocolCommand, ISingleton<HeartbeatCommand>
{
    public static HeartbeatCommand Instance { get; } = new();
    
    private HeartbeatCommand()
    {
    }

    public override int Write(Span<byte> buffer) => FragmentHeader.WriteHeartbeat(buffer);
}

internal abstract class EmptyPayloadCommand(ulong streamId, [ConstantExpected] FragmentControl control) : ProtocolCommand
{
    public sealed override int Write(Span<byte> buffer)
    {
        var header = new FragmentHeader(streamId, control, 0);
        header.Format(buffer);
        return FragmentHeader.Size;
    }
}

internal sealed class StreamRejectedCommand(ulong streamId) : EmptyPayloadCommand(streamId, FragmentControl.StreamRejected);

internal sealed class StreamClosedCommand(ulong streamId) : EmptyPayloadCommand(streamId, FragmentControl.StreamClosed);