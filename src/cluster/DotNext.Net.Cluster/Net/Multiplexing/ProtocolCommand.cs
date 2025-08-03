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

internal sealed class StreamRejectedCommand(ulong streamId) : ProtocolCommand
{
    public override int Write(Span<byte> buffer)
    {
        var header = new FragmentHeader(streamId, FragmentControl.StreamRejected, 0);
        header.Format(buffer);
        return FragmentHeader.Size;
    }
}