namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class ProtocolStream
{
    internal ValueTask WriteVoteRequestAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Write((byte)MessageType.Vote);
        VoteMessage.Write(ref writer, term, lastLogIndex, lastLogTerm);
        return transport.WriteAsync(buffer.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WritePreVoteRequestAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Write((byte)MessageType.PreVote);
        PreVoteMessage.Write(ref writer, term, lastLogIndex, lastLogTerm);
        return transport.WriteAsync(buffer.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WriteResponseAsync(in Result<bool> result, CancellationToken token)
    {
        var writer = new SpanWriter<byte>(buffer.Span);
        Result.Write(ref writer, in result);
        return transport.WriteAsync(buffer.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WriteResponseAsync(bool value, CancellationToken token)
    {
        var buffer = this.buffer.Slice(0, 1);
        buffer.Span[0] = value.ToByte();
        return transport.WriteAsync(buffer, token);
    }

    internal ValueTask WriteResponseAsync(long? value, CancellationToken token)
    {
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Add(value.HasValue.ToByte());
        writer.WriteInt64(value.GetValueOrDefault(), true);
        return transport.WriteAsync(buffer.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WriteResignRequestAsync(CancellationToken token)
    {
        var buffer = this.buffer.Slice(0, 1);
        buffer.Span[0] = (byte)MessageType.Resign;
        return transport.WriteAsync(buffer, token);
    }

    internal ValueTask WriteSynchronizeRequestAsync(CancellationToken token)
    {
        var buffer = this.buffer.Slice(0, 1);
        buffer.Span[0] = (byte)MessageType.Synchronize;
        return transport.WriteAsync(buffer, token);
    }

    internal ValueTask WriteMetadataRequestAsync(CancellationToken token)
    {
        var buffer = this.buffer.Slice(0, 1);
        buffer.Span[0] = (byte)MessageType.Metadata;
        return transport.WriteAsync(buffer, token);
    }

    internal async ValueTask WriteMetadataResponseAsync(IReadOnlyDictionary<string, string> metadata, Memory<byte> buffer, CancellationToken token)
    {
        PrepareForWrite();
        await DataTransferObject.WriteToAsync(new MetadataTransferObject(metadata), this, buffer, token).ConfigureAwait(false);
        await WriteFinalFrameAsync(token).ConfigureAwait(false);
    }

    internal async ValueTask WriteInstallSnapshotRequestAsync(long term, long snapshotIndex, IRaftLogEntry snapshot, Memory<byte> buffer, CancellationToken token)
    {
        Reset();
        PrepareForWrite(WriteHeaders(this.buffer.Span, term, snapshotIndex, snapshot));
        await snapshot.WriteToAsync(this, buffer, token).ConfigureAwait(false);
        await WriteFinalFrameAsync(token).ConfigureAwait(false);

        static int WriteHeaders(Span<byte> buffer, long term, long snapshotIndex, IRaftLogEntry snapshot)
        {
            var writer = new SpanWriter<byte>(buffer);
            writer.Add((byte)MessageType.InstallSnapshot);
            SnapshotMessage.Write(ref writer, term, snapshotIndex, snapshot);
            return writer.WrittenCount;
        }
    }

    internal async ValueTask WriteAppendEntriesRequestAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {

    }

    internal async ValueTask WriteConfigurationRequestAsync(IClusterConfiguration configuration, Memory<byte> buffer, CancellationToken token)
    {
        Reset();
        PrepareForWrite(WriteHeaders(this.buffer.Span, configuration));
        await configuration.WriteToAsync(this, buffer, token).ConfigureAwait(false);
        await WriteFinalFrameAsync(token).ConfigureAwait(false);

        static int WriteHeaders(Span<byte> buffer, IClusterConfiguration configuration)
        {
            var writer = new SpanWriter<byte>(buffer);
            writer.Add((byte)MessageType.Configuration);
            ConfigurationMessage.Write(ref writer, configuration.Fingerprint, configuration.Length);
            return writer.WrittenCount;
        }
    }

    internal void PrepareForWrite(int offset = 0)
    {
        bufferStart = bufferEnd = offset + FrameHeadersSize;
    }

    private void WriteFrameHeaders(int chunkSize, bool finalBlock)
    {
        var writer = new SpanWriter<byte>(buffer.Span.Slice(bufferStart - FrameHeadersSize, FrameHeadersSize));
        writer.WriteInt32(chunkSize, true);
        writer.Add(finalBlock.ToByte());
        bufferStart += writer.WrittenCount;
    }

    private int WriteToBuffer(ReadOnlySpan<byte> input)
    {
        input.CopyTo(buffer.Span.Slice(bufferEnd), out var writtenCount);
        bufferEnd += writtenCount;
        return writtenCount;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token = default)
    {
        while (!input.IsEmpty)
        {
            input = input.Slice(WriteToBuffer(input.Span));

            // write frame to the transport layer
            if (bufferEnd == buffer.Length)
            {
                // write frame size
                WriteFrameHeaders(AvailableBytes, finalBlock: false);
                await transport.WriteAsync(buffer, token).ConfigureAwait(false);
                bufferEnd = bufferStart = FrameHeadersSize;
            }
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), token).AsTask();

    public override void Write(ReadOnlySpan<byte> input)
    {
        while (!input.IsEmpty)
        {
            input = input.Slice(WriteToBuffer(input));

            // write frame to the transport layer
            if (bufferEnd == buffer.Length)
            {
                // write frame size
                WriteFrameHeaders(AvailableBytes, finalBlock: false);
                transport.Write(buffer.Span);
                bufferEnd = bufferStart = FrameHeadersSize;
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override Task FlushAsync(CancellationToken token)
        => WriteFinalFrameAsync(token).AsTask();

    internal ValueTask WriteFinalFrameAsync(CancellationToken token)
    {
        WriteFrameHeaders(AvailableBytes, finalBlock: true);
        return transport.WriteAsync(buffer.Slice(0, bufferEnd));
    }

    public override void Flush()
    {
        WriteFrameHeaders(AvailableBytes, finalBlock: true);
        transport.Write(buffer.Span.Slice(0, bufferEnd));
    }
}