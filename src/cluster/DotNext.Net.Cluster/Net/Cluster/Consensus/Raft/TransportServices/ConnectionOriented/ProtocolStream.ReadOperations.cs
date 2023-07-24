using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using Serializable = Runtime.Serialization.Serializable;

internal partial class ProtocolStream
{
    private enum ReadState
    {
        FrameNotStarted = 0,
        FrameStarted,
        EndOfStreamReached,
    }

    private ReadState readState;
    private int frameSize;

    private ValueTask BufferizeAsync(int count, CancellationToken token)
    {
        if (bufferEnd - bufferStart >= count)
            return ValueTask.CompletedTask;

        var freeCapacity = buffer.Length - bufferEnd;

        if (bufferStart + freeCapacity < count)
            throw new InternalBufferOverflowException();

        if (freeCapacity < count)
        {
            buffer.Span[bufferStart..bufferEnd].CopyTo(buffer.Span);
            bufferEnd -= bufferStart;
            bufferStart = 0;
        }

        return BufferizeSlowAsync(count, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask BufferizeSlowAsync(int count, CancellationToken token)
    {
        Debug.Assert(bufferEnd < this.buffer.Length);

        var buffer = this.buffer.Memory.Slice(bufferEnd);
        bufferEnd += await ReadFromTransportAsync(count, buffer, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<T> ReadAsync<T>(int count, IntPtr decoder, CancellationToken token)
    {
        await BufferizeAsync(count, token).ConfigureAwait(false);
        var result = Read(buffer.Span.Slice(bufferStart), decoder);
        bufferStart += count;
        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe T Read(ReadOnlySpan<byte> input, IntPtr decoder)
        {
            var reader = new SpanReader<byte>(input);
            return ((delegate*<ref SpanReader<byte>, T>)decoder)(ref reader);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe ValueTask<T> ReadAsync<T>(int count, delegate*<ref SpanReader<byte>, T> decoder, CancellationToken token)
        => ReadAsync<T>(count, (IntPtr)decoder, token);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal async ValueTask<MessageType> ReadMessageTypeAsync(CancellationToken token)
    {
        Debug.Assert(bufferStart is 0);
        Debug.Assert(bufferEnd is 0);

        // in case of SslStream, ReadAsync will return 0 bytes and we don't want to throw an error
        var buffer = this.buffer.Memory;
        MessageType result;

        if ((bufferEnd = await ReadFromTransportAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            bufferStart = 1;
            result = (MessageType)MemoryMarshal.GetReference(buffer.Span);
        }
        else
        {
            result = MessageType.None;
        }

        return result;
    }

    internal unsafe ValueTask<(ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm)> ReadVoteRequestAsync(CancellationToken token)
        => ReadAsync<(ClusterMemberId, long, long, long)>(VoteMessage.Size, &VoteMessage.Read, token);

    internal unsafe ValueTask<(ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm)> ReadPreVoteRequestAsync(CancellationToken token)
        => ReadAsync<(ClusterMemberId, long, long, long)>(PreVoteMessage.Size, &PreVoteMessage.Read, token);

    internal unsafe ValueTask<Result<bool>> ReadResultAsync(CancellationToken token)
        => ReadAsync<Result<bool>>(Result.Size, &Result.Read, token);

    internal unsafe ValueTask<Result<bool?>> ReadNullableResultAsync(CancellationToken token)
        => ReadAsync<Result<bool?>>(Result.Size, &Result.ReadNullable, token);

    internal unsafe ValueTask<Result<PreVoteResult>> ReadPreVoteResultAsync(CancellationToken token)
        => ReadAsync<Result<PreVoteResult>>(Result.Size, &Result.ReadPreVoteResult, token);

    internal unsafe ValueTask<bool> ReadBoolAsync(CancellationToken token)
    {
        return ReadAsync<bool>(sizeof(byte), &Read, token);

        static bool Read(ref SpanReader<byte> reader) => ValueTypeExtensions.ToBoolean(reader.Read());
    }

    internal unsafe ValueTask<long?> ReadNullableInt64Async(CancellationToken token)
    {
        return ReadAsync<long?>(sizeof(long) + sizeof(byte), &Read, token);

        static long? Read(ref SpanReader<byte> reader)
        {
            var hasValue = ValueTypeExtensions.ToBoolean(reader.Read());
            var value = reader.ReadInt64(true);
            return hasValue ? value : null;
        }
    }

    internal unsafe ValueTask<long> ReadInt64Async(CancellationToken token)
    {
        return ReadAsync<long>(sizeof(long), &Read, token);

        static long Read(ref SpanReader<byte> reader) => reader.ReadInt64(true);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal async ValueTask<IReadOnlyDictionary<string, string>> ReadMetadataResponseAsync(Memory<byte> buffer, CancellationToken token)
#pragma warning disable CA2252  // TODO: Remove in .NET 7
        => (await Serializable.ReadFromAsync<MetadataTransferObject>(this, buffer, token).ConfigureAwait(false)).Metadata;
#pragma warning restore CA2252

    internal async ValueTask<(ClusterMemberId Id, long Term, long SnapshotIndex, ReceivedSnapshot Snapshot)> ReadInstallSnapshotRequestAsync(CancellationToken token)
    {
        var result = await ReadRequestAsync().ConfigureAwait(false);
        return new()
        {
            Id = result.Id,
            Term = result.Term,
            SnapshotIndex = result.SnapshotIndex,
            Snapshot = new(this, in result.SnapshotMetadata),
        };

        unsafe ValueTask<(ClusterMemberId Id, long Term, long SnapshotIndex, LogEntryMetadata SnapshotMetadata)> ReadRequestAsync()
            => ReadAsync<(ClusterMemberId, long, long, LogEntryMetadata)>(SnapshotMessage.Size, &SnapshotMessage.Read, token);
    }

    internal async ValueTask<(ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, ReceivedLogEntries Entries, InMemoryClusterConfiguration Configuration, bool ApplyConfig)> ReadAppendEntriesRequestAsync(CancellationToken token)
    {
        // read headers
        var result = await ReadHeadersAsync().ConfigureAwait(false);

        // load configuration
        MemoryOwner<byte> config;
        if (result.ConfigLength > 0)
        {
            config = allocator.Invoke(checked((int)result.ConfigLength), exactSize: true);
            await this.ReadAtLeastAsync(config.Length, config.Memory, token).ConfigureAwait(false);
        }
        else
        {
            config = default;
        }

        readState = ReadState.FrameNotStarted;
        frameSize = 0;

        return new()
        {
            Id = result.Id,
            Term = result.Term,
            PrevLogIndex = result.PrevLogIndex,
            PrevLogTerm = result.PrevLogTerm,
            CommitIndex = result.CommitIndex,
            ApplyConfig = result.ApplyConfig,
            Configuration = new(config, result.Fingerprint),
            Entries = new ReceivedLogEntries(this, result.EntriesCount, token),
        };

        unsafe ValueTask<(ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount, bool ApplyConfig, long Fingerprint, long ConfigLength)> ReadHeadersAsync()
            => ReadAsync<(ClusterMemberId, long Term, long PrevLogIndex, long, long, int, bool, long, long)>(AppendEntriesHeadersSize, &ReadHeaders, token);

        static unsafe (ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount, bool ApplyConfig, long Fingerprint, long ConfigLength) ReadHeaders(ref SpanReader<byte> reader)
        {
            (ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount, bool ApplyConfig, long Fingerprint, long ConfigLength) result;
            (result.Id, result.Term, result.PrevLogIndex, result.PrevLogTerm, result.CommitIndex, result.EntriesCount) = AppendEntriesMessage.Read(ref reader);
            result.ApplyConfig = ValueTypeExtensions.ToBoolean(reader.Read());
            result.Fingerprint = reader.ReadInt64(true);
            result.ConfigLength = reader.ReadInt64(true);
            return result;
        }
    }

    private void BeginReadFrame()
    {
        if (bufferStart == bufferEnd)
        {
            bufferStart = bufferEnd = 0;
        }
        else
        {
            // shift written buffer to the left
            buffer.Span[bufferStart..bufferEnd].CopyTo(buffer.Span);
            bufferEnd -= bufferStart;
            bufferStart = 0;
        }
    }

    private void EndReadFrame()
    {
        (frameSize, var finalBlock) = ParseFrameHeaders(buffer.Span);
        readState = finalBlock ? ReadState.EndOfStreamReached : ReadState.FrameStarted;
        bufferStart += FrameHeadersSize;

        static (int, bool) ParseFrameHeaders(ReadOnlySpan<byte> input)
        {
            var reader = new SpanReader<byte>(input);
            return (reader.ReadInt32(true), ValueTypeExtensions.ToBoolean(reader.Read()));
        }
    }

    private void StartFrame()
    {
        BeginReadFrame();

        // how much bytes we should read from the stream to parse the frame headers
        var frameHeaderRemainingBytes = FrameHeadersSize - bufferEnd;

        // frame header is not yet in the buffer
        if (frameHeaderRemainingBytes > 0)
            bufferEnd = ReadFromTransport(frameHeaderRemainingBytes, buffer.Span.Slice(bufferEnd));

        EndReadFrame();
    }

    private int ReadFrame(Span<byte> output)
    {
        var count = Math.Min(frameSize, bufferEnd - bufferStart);
        if (count > 0)
        {
            buffer.Span.Slice(bufferStart, count).CopyTo(output, out count);
            bufferStart += count;
            frameSize -= count;
        }

        return count;
    }

    private void SkipFrame()
    {
        var count = Math.Min(frameSize, bufferEnd - bufferStart);
        bufferStart += count;
        frameSize -= count;
    }

    public sealed override int Read(Span<byte> output)
    {
    check_state:
        switch ((readState, frameSize is 0))
        {
            case (ReadState.FrameStarted, true):
            case (ReadState.FrameNotStarted, _):
                StartFrame();
                goto check_state; // skip empty frames
            case (ReadState.EndOfStreamReached, true):
                return 0;
            default:
                if (bufferStart == bufferEnd)
                {
                    bufferStart = 0;
                    bufferEnd = ReadFromTransport(buffer.Span);
                }

                // we can copy no more than remaining frame
                return ReadFrame(output);
        }
    }

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    private async ValueTask StartFrameAsync(CancellationToken token)
    {
        BeginReadFrame();

        // how much bytes we should read from the stream to parse the frame headers
        var frameHeaderRemainingBytes = FrameHeadersSize - bufferEnd;

        // frame header is not yet in the buffer
        if (frameHeaderRemainingBytes > 0)
            bufferEnd = await ReadFromTransportAsync(frameHeaderRemainingBytes, buffer.Memory.Slice(bufferEnd), token).ConfigureAwait(false);

        EndReadFrame();
    }

    public sealed override async ValueTask<int> ReadAsync(Memory<byte> output, CancellationToken token)
    {
        check_state:
        switch ((readState, frameSize is 0))
        {
            case (ReadState.FrameStarted, true):
            case (ReadState.FrameNotStarted, _):
                await StartFrameAsync(token).ConfigureAwait(false);
                goto check_state; // skip empty frames
            case (ReadState.EndOfStreamReached, true):
                return 0;
            default:
                if (bufferStart == bufferEnd)
                {
                    bufferStart = 0;
                    bufferEnd = await ReadFromTransportAsync(buffer.Memory, token).ConfigureAwait(false);
                }

                // we can copy no more than remaining frame
                return ReadFrame(output.Span);
        }
    }

    public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

    internal async ValueTask SkipAsync(CancellationToken token)
    {
        while (true)
        {
            switch ((readState, frameSize is 0))
            {
                case (ReadState.FrameStarted, true):
                case (ReadState.FrameNotStarted, _):
                    await StartFrameAsync(token).ConfigureAwait(false);
                    continue; // skip empty frames
                case (ReadState.EndOfStreamReached, true):
                    return;
                default:
                    if (bufferStart == bufferEnd)
                    {
                        bufferStart = 0;
                        bufferEnd = await ReadFromTransportAsync(buffer.Memory, token).ConfigureAwait(false);
                    }

                    SkipFrame();
                    continue;
            }
        }
    }
}