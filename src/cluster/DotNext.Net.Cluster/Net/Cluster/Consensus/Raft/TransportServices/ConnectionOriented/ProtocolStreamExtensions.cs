using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using Serializable = Runtime.Serialization.Serializable;

internal static class ProtocolStreamExtensions
{
    internal static ValueTask WriteBoolResultAsync(this ProtocolStream protocol, in Result<bool> result, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        Result.Write(ref writer, in result);
        protocol.AdvanceWriteCursor(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static async ValueTask<Result<bool>> ReadBoolResultAsync(this ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(Result.Size, token).ConfigureAwait(false);
        return Result.Read(protocol.WrittenBufferSpan);
    }

    internal static ValueTask WriteHeartbeatResultAsync(this ProtocolStream protocol, in Result<HeartbeatResult> result, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        Result.WriteHeartbeatResult(ref writer, in result);
        protocol.AdvanceWriteCursor(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal static async ValueTask<Result<HeartbeatResult>> ReadHeartbeatResultAsync(this ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(Result.Size, token).ConfigureAwait(false);
        return Result.ReadHeartbeatResult(protocol.WrittenBufferSpan);
    }

    internal static ValueTask WritePreVoteResultAsync(this ProtocolStream protocol, in Result<PreVoteResult> result, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        Result.WritePreVoteResult(ref writer, in result);
        protocol.AdvanceWriteCursor(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static async ValueTask<Result<PreVoteResult>> ReadPreVoteResultAsync(this ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(Result.Size, token).ConfigureAwait(false);
        return Result.ReadPreVoteResult(protocol.WrittenBufferSpan);
    }

    internal static ValueTask WriteBoolAsync(this ProtocolStream protocol, bool value, CancellationToken token)
    {
        protocol.Reset();
        protocol.RemainingBufferSpan[0] = value.ToByte();
        protocol.AdvanceWriteCursor(1);
        return protocol.WriteToTransportAsync(token);
    }

    internal static async ValueTask<bool> ReadBoolAsync(this ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(sizeof(byte), token).ConfigureAwait(false);
        return BasicExtensions.ToBoolean(protocol.WrittenBufferSpan[0]);
    }

    internal static ValueTask WriteNullableInt64Async(this ProtocolStream protocol, in long? value, CancellationToken token)
    {
        protocol.Reset();
        var writer = new SpanWriter<byte>(protocol.RemainingBufferSpan);
        writer.Add(value.HasValue.ToByte());
        writer.WriteLittleEndian(value.GetValueOrDefault());
        protocol.AdvanceWriteCursor(writer.WrittenCount);
        return protocol.WriteToTransportAsync(token);
    }

    internal static async ValueTask<long?> ReadNullableInt64Async(this ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(sizeof(long) + sizeof(byte), token).ConfigureAwait(false);
        return Read(protocol.WrittenBufferSpan);

        static long? Read(ReadOnlySpan<byte> responseData)
        {
            var reader = new SpanReader<byte>(responseData);
            return reader.Read() is 0 ? null : reader.ReadLittleEndian<long>(isUnsigned: false);
        }
    }

    internal static async ValueTask WriteDictionaryAsync(this ProtocolStream protocol, IReadOnlyDictionary<string, string> metadata, Memory<byte> buffer, CancellationToken token)
    {
        protocol.Reset();
        protocol.StartFrameWrite();
        await DataTransferObject.WriteToAsync(new MetadataTransferObject(metadata), protocol, buffer, token).ConfigureAwait(false);
        protocol.WriteFinalFrame();
        await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
    }

    internal static async ValueTask<IReadOnlyDictionary<string, string>> ReadDictionaryAsync(this ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
#pragma warning disable CA2252  // TODO: Remove in .NET 7
        => (await Serializable.ReadFromAsync<MetadataTransferObject>(protocol, buffer, token).ConfigureAwait(false)).Metadata;
#pragma warning restore CA2252
}