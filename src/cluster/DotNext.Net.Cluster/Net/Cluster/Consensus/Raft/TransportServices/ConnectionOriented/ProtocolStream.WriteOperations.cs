namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class ProtocolStream
{
    internal ValueTask WriteVoteRequestAsync(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        Reset();
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Write((byte)MessageType.Vote);
        VoteMessage.Write(ref writer, in sender, term, lastLogIndex, lastLogTerm);
        return WriteToTransportAsync(buffer.Memory.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WritePreVoteRequestAsync(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        Reset();
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Write((byte)MessageType.PreVote);
        PreVoteMessage.Write(ref writer, in sender, term, lastLogIndex, lastLogTerm);
        return WriteToTransportAsync(buffer.Memory.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WriteResponseAsync(in Result<bool> result, CancellationToken token)
    {
        Reset();
        return WriteToTransportAsync(buffer.Memory.Slice(0, Result.Write(buffer.Span, in result)), token);
    }

    internal ValueTask WriteResponseAsync(in Result<HeartbeatResult> result, CancellationToken token)
    {
        Reset();
        return WriteToTransportAsync(buffer.Memory.Slice(0, Result.WriteHeartbeatResult(buffer.Span, in result)), token);
    }

    internal ValueTask WriteResponseAsync(in Result<PreVoteResult> result, CancellationToken token)
    {
        Reset();
        return WriteToTransportAsync(buffer.Memory.Slice(0, Result.WritePreVoteResult(buffer.Span, in result)), token);
    }

    internal ValueTask WriteResponseAsync(bool value, CancellationToken token)
    {
        Reset();
        var buffer = this.buffer.Memory.Slice(0, 1);
        buffer.Span[0] = value.ToByte();
        return WriteToTransportAsync(buffer, token);
    }

    internal ValueTask WriteResponseAsync(in long? value, CancellationToken token)
    {
        Reset();
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Add(value.HasValue.ToByte());
        writer.WriteInt64(value.GetValueOrDefault(), true);
        return WriteToTransportAsync(buffer.Memory.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WriteResignRequestAsync(CancellationToken token)
    {
        Reset();
        var buffer = this.buffer.Memory.Slice(0, 1);
        buffer.Span[0] = (byte)MessageType.Resign;
        return WriteToTransportAsync(buffer, token);
    }

    internal ValueTask WriteSynchronizeRequestAsync(long commitIndex, CancellationToken token)
    {
        Reset();
        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Write((byte)MessageType.Synchronize);
        writer.WriteInt64(commitIndex, true);
        return WriteToTransportAsync(buffer.Memory.Slice(0, writer.WrittenCount), token);
    }

    internal ValueTask WriteMetadataRequestAsync(CancellationToken token)
    {
        Reset();
        var buffer = this.buffer.Memory.Slice(0, 1);
        buffer.Span[0] = (byte)MessageType.Metadata;
        return WriteToTransportAsync(buffer, token);
    }

    internal async ValueTask WriteMetadataResponseAsync(IReadOnlyDictionary<string, string> metadata, Memory<byte> buffer, CancellationToken token)
    {
        PrepareForWrite();
        await DataTransferObject.WriteToAsync(new MetadataTransferObject(metadata), this, buffer, token).ConfigureAwait(false);
        WriteFinalFrame();
        await FlushAsync(token).ConfigureAwait(false);
    }

    internal async ValueTask WriteInstallSnapshotRequestAsync(ClusterMemberId sender, long term, long snapshotIndex, IRaftLogEntry snapshot, Memory<byte> buffer, CancellationToken token)
    {
        Reset();
        PrepareForWrite(bufferEnd = WriteHeaders(this.buffer.Span, in sender, term, snapshotIndex, snapshot));
        await snapshot.WriteToAsync(this, buffer, token).ConfigureAwait(false);
        WriteFinalFrame();
        await FlushAsync(token).ConfigureAwait(false);

        static int WriteHeaders(Span<byte> buffer, in ClusterMemberId sender, long term, long snapshotIndex, IRaftLogEntry snapshot)
        {
            var writer = new SpanWriter<byte>(buffer);
            writer.Add((byte)MessageType.InstallSnapshot);
            SnapshotMessage.Write(ref writer, in sender, term, snapshotIndex, snapshot);
            return writer.WrittenCount;
        }
    }

    internal async ValueTask WriteAppendEntriesRequestAsync<TEntry, TList>(ClusterMemberId sender, long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, Memory<byte> buffer, CancellationToken token)
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {
        Reset();
        bufferEnd = WriteHeaders(this.buffer.Span, in sender, term, prevLogIndex, prevLogTerm, commitIndex, entries.Count, applyConfig, config.Fingerprint, config.Length);

        // write configuration
        if (config.Length > 0L)
        {
            PrepareForWrite(bufferEnd);
            await config.WriteToAsync(this, buffer, token).ConfigureAwait(false);
            WriteFinalFrame();
        }

        // write log entries
        foreach (var entry in entries)
        {
            if (this.buffer.Length - bufferEnd < LogEntryMetadata.Size + FrameHeadersSize + 1)
                await FlushAsync(token).ConfigureAwait(false);

            PrepareForWrite(bufferEnd += WriteLogEntryMetadata(this.buffer.Span.Slice(bufferEnd), entry));
            await entry.WriteToAsync(this, buffer, token).ConfigureAwait(false);
            WriteFinalFrame();
        }

        await FlushAsync(token).ConfigureAwait(false);

        static int WriteHeaders(Span<byte> buffer, in ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex, int entriesCount, bool applyConfig, long fingerprint, long configLength)
        {
            var writer = new SpanWriter<byte>(buffer);
            writer.Add((byte)MessageType.AppendEntries);
            AppendEntriesMessage.Write(ref writer, in sender, term, prevLogIndex, prevLogTerm, commitIndex, entriesCount);
            writer.Add(applyConfig.ToByte());
            writer.WriteInt64(fingerprint, true);
            writer.WriteInt64(configLength, true);
            return writer.WrittenCount;
        }

        static int WriteLogEntryMetadata(Span<byte> buffer, TEntry entry)
        {
            var writer = new SpanWriter<byte>(buffer);
            LogEntryMetadata.Create(entry).Format(ref writer);
            return writer.WrittenCount;
        }
    }

    internal void PrepareForWrite(int offset = 0)
        => bufferEnd = (bufferStart = offset) + FrameHeadersSize;

    private void WriteFrameHeaders(int chunkSize, bool finalBlock)
    {
        var writer = new SpanWriter<byte>(buffer.Span.Slice(bufferStart));
        writer.WriteInt32(chunkSize, true);
        writer.Add(finalBlock.ToByte());
    }

    private int WriteToBuffer(ReadOnlySpan<byte> input)
    {
        input.CopyTo(buffer.Span.Slice(bufferEnd), out var writtenCount);
        bufferEnd += writtenCount;
        return writtenCount;
    }

    public sealed override async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token = default)
    {
        while (!input.IsEmpty)
        {
            input = input.Slice(WriteToBuffer(input.Span));

            // write frame to the transport layer
            if (bufferEnd == buffer.Length)
            {
                // write frame size
                WriteFrameHeaders(bufferEnd - bufferStart - FrameHeadersSize, finalBlock: false);
                await WriteToTransportAsync(buffer.Memory, token).ConfigureAwait(false);
                bufferStart = 0;
                bufferEnd = FrameHeadersSize;
            }
        }
    }

    public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), token).AsTask();

    public sealed override void Write(ReadOnlySpan<byte> input)
    {
        while (!input.IsEmpty)
        {
            input = input.Slice(WriteToBuffer(input));

            // write frame to the transport layer
            if (bufferEnd == buffer.Length)
            {
                // write frame size
                WriteFrameHeaders(bufferEnd - bufferStart - FrameHeadersSize, finalBlock: false);
                WriteToTransport(buffer.Span);
                bufferStart = 0;
                bufferEnd = FrameHeadersSize;
            }
        }
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    private async Task FlushCoreAsync(CancellationToken token)
    {
        await WriteToTransportAsync(buffer.Memory.Slice(0, bufferEnd), token).ConfigureAwait(false);
        bufferStart = bufferEnd = 0;
    }

    public sealed override Task FlushAsync(CancellationToken token)
        => bufferEnd > 0 ? FlushCoreAsync(token) : Task.CompletedTask;

    internal void WriteFinalFrame()
    {
        WriteFrameHeaders(bufferEnd - bufferStart - FrameHeadersSize, finalBlock: true);
        bufferStart = bufferEnd;
    }

    public sealed override void Flush()
    {
        WriteToTransport(buffer.Span.Slice(0, bufferEnd));
        bufferStart = bufferEnd = 0;
    }
}