namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class Result
{
    internal const int Size = sizeof(long) + sizeof(byte);

    internal static void Write(ref SpanWriter<byte> writer, in Result<bool> result)
    {
        writer.WriteInt64(result.Term, true);
        writer.Add(result.Value.ToByte());
    }

    internal static int Write(Span<byte> output, in Result<bool> result)
    {
        var writer = new SpanWriter<byte>(output);
        Write(ref writer, in result);
        return writer.WrittenCount;
    }

    internal static void WritePreVoteResult(ref SpanWriter<byte> writer, in Result<PreVoteResult> result)
    {
        writer.WriteInt64(result.Term, true);
        writer.Add((byte)result.Value);
    }

    internal static int WritePreVoteResult(Span<byte> output, in Result<PreVoteResult> result)
    {
        var writer = new SpanWriter<byte>(output);
        WritePreVoteResult(ref writer, in result);
        return writer.WrittenCount;
    }

    internal static void WriteHeartbeatResult(ref SpanWriter<byte> writer, in Result<HeartbeatResult> result)
    {
        writer.WriteInt64(result.Term, true);
        writer.Add((byte)result.Value);
    }

    internal static int WriteHeartbeatResult(Span<byte> output, in Result<HeartbeatResult> result)
    {
        var writer = new SpanWriter<byte>(output);
        WriteHeartbeatResult(ref writer, in result);
        return writer.WrittenCount;
    }

    internal static Result<bool> Read(ref SpanReader<byte> reader)
        => new(reader.ReadInt64(true), ValueTypeExtensions.ToBoolean(reader.Read()));

    internal static Result<bool> Read(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return Read(ref reader);
    }

    internal static Result<PreVoteResult> ReadPreVoteResult(ref SpanReader<byte> reader)
        => new(reader.ReadInt64(true), (PreVoteResult)reader.Read());

    internal static Result<PreVoteResult> ReadPreVoteResult(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return ReadPreVoteResult(ref reader);
    }

    internal static Result<HeartbeatResult> ReadHeartbeatResult(ref SpanReader<byte> reader)
        => new(reader.ReadInt64(true), (HeartbeatResult)reader.Read());

    internal static Result<HeartbeatResult> ReadHeartbeatResult(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return ReadHeartbeatResult(ref reader);
    }
}