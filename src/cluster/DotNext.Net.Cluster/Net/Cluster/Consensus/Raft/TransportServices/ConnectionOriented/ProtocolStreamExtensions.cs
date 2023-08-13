namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;

internal static class ProtocolStreamExtensions
{
    internal static ValueTask WriteResponseAsync(this ProtocolStream protocol, in Result<bool> result, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        Result.Write(ref writer, in result);
        protocol.Advance(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static ValueTask WriteResponseAsync(this ProtocolStream protocol, in Result<HeartbeatResult> result, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        Result.WriteHeartbeatResult(ref writer, in result);
        protocol.Advance(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static ValueTask WriteResponseAsync(this ProtocolStream protocol, in Result<PreVoteResult> result, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        Result.WritePreVoteResult(ref writer, in result);
        protocol.Advance(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static ValueTask WriteResponseAsync(this ProtocolStream protocol, bool value, CancellationToken token)
    {
        protocol.Reset();
        protocol.RemainingBufferSpan[0] = value.ToByte();
        protocol.Advance(1);
        return protocol.WriteToTransportAsync(token);
    }

    internal static ValueTask WriteResponseAsync(this ProtocolStream protocol, in long? value, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        writer.Add(value.HasValue.ToByte());
        writer.WriteInt64(value.GetValueOrDefault(), true);
        protocol.Advance(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static async ValueTask WriteMetadataResponseAsync(this ProtocolStream protocol, IReadOnlyDictionary<string, string> metadata, Memory<byte> buffer, CancellationToken token)
    {
        protocol.Reset();
        protocol.StartFrameWrite();
        await DataTransferObject.WriteToAsync(new MetadataTransferObject(metadata), protocol, buffer, token).ConfigureAwait(false);
        protocol.WriteFinalFrame();
        await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
    }
}