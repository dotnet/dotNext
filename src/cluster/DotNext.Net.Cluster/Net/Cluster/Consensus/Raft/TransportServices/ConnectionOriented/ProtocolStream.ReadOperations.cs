using System.Runtime.CompilerServices;
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
            buffer[bufferStart..bufferEnd].CopyTo(buffer);
            bufferEnd -= bufferStart;
            bufferStart = 0;
        }

        return BufferizeSlowAsync();

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
        async ValueTask BufferizeSlowAsync()
        {
            Debug.Assert(bufferEnd < this.buffer.Length);

            var buffer = this.buffer.Slice(bufferEnd);
            bufferEnd += await transport.ReadAtLeastAsync(count, buffer, token).ConfigureAwait(false);
        }
    }

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

    internal unsafe ValueTask<MessageType> ReadMessageTypeAsync(CancellationToken token)
    {
        return ReadAsync<MessageType>(sizeof(MessageType), &Read, token);

        static MessageType Read(ref SpanReader<byte> reader) => reader.Read<MessageType>();
    }

    internal unsafe ValueTask<(ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm)> ReadVoteRequestAsync(CancellationToken token)
        => ReadAsync<(ClusterMemberId, long, long, long)>(VoteMessage.Size, &VoteMessage.Read, token);

    internal unsafe ValueTask<(ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm)> ReadPreVoteRequestAsync(CancellationToken token)
        => ReadAsync<(ClusterMemberId, long, long, long)>(PreVoteMessage.Size, &PreVoteMessage.Read, token);

    internal unsafe ValueTask<Result<bool>> ReadResultAsync(CancellationToken token)
        => ReadAsync<Result<bool>>(Result.Size, &Result.Read, token);

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

    [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal async ValueTask<IReadOnlyDictionary<string, string>> ReadMetadataResponseAsync(Memory<byte> buffer, CancellationToken token)
#pragma warning disable CA2252  // TODO: Remove in .NET 7
        => (await Serializable.ReadFromAsync<MetadataTransferObject>(this, buffer, token).ConfigureAwait(false)).Metadata;
#pragma warning restore CA2252

    internal unsafe ValueTask<(ClusterMemberId Id, long Term, long SnapshotIndex, LogEntryMetadata SnapshotMetadata)> ReadInstallSnapshotRequest(CancellationToken token)
        => ReadAsync<(ClusterMemberId, long, long, LogEntryMetadata)>(SnapshotMessage.Size, &SnapshotMessage.Read, token);

    internal IRaftLogEntry CreateSnapshot(in LogEntryMetadata metadata) => new Snapshot(this, in metadata);

    internal async ValueTask<(ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex)> ReadAppendEntriesRequestAsync(CancellationToken token)
    {
        var result = await ReadCoreAsync().ConfigureAwait(false);
        entriesCount = result.EntriesCount;
        return new() { Id = result.Id, Term = result.Term, PrevLogIndex = result.PrevLogIndex, PrevLogTerm = result.PrevLogTerm };

        unsafe ValueTask<(ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount)> ReadCoreAsync()
            => ReadAsync<(ClusterMemberId, long, long, long, long, int)>(AppendEntriesMessage.Size, &AppendEntriesMessage.Read, token);
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
            buffer[bufferStart..bufferEnd].CopyTo(buffer);
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
            bufferEnd = transport.ReadAtLeast(frameHeaderRemainingBytes, buffer.Span.Slice(bufferEnd));

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

    public override int Read(Span<byte> output)
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
                    bufferEnd = transport.Read(buffer.Span);
                }

                // we can copy no more than remaining frame
                return ReadFrame(output);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
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
            bufferEnd = await transport.ReadAtLeastAsync(frameHeaderRemainingBytes, buffer.Slice(bufferEnd), token).ConfigureAwait(false);

        EndReadFrame();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> output, CancellationToken token)
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
                    bufferEnd = transport.Read(buffer.Span);
                }

                // we can copy no more than remaining frame
                return ReadFrame(output.Span);
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
}