using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;

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

    internal ValueTask ReadAsync(int count, CancellationToken token)
    {
        if (bufferEnd - bufferStart >= count)
            return ValueTask.CompletedTask;

        var freeCapacity = buffer.Length - bufferEnd;

        if (bufferStart + freeCapacity < count)
            return ValueTask.FromException(new InternalBufferOverflowException());

        if (freeCapacity < count)
        {
            WrittenBufferSpan.CopyTo(buffer.Span);
            bufferEnd -= bufferStart;
            bufferStart = 0;
        }

        return BufferizeSlowAsync(count, token);
    }

    internal void AdvanceReadCursor(int count)
        => bufferStart += count;

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask BufferizeSlowAsync(int count, CancellationToken token)
    {
        Debug.Assert(bufferEnd < buffer.Length);

        bufferEnd += await ReadFromTransportAsync(count, RemainingBuffer, token).ConfigureAwait(false);
    }

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

    private void BeginReadFrameHeader()
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

    private void EndReadFrameHeader()
    {
        (frameSize, var finalBlock) = ReadFrameHeaders(buffer.Span);
        readState = finalBlock ? ReadState.EndOfStreamReached : ReadState.FrameStarted;
        bufferStart += FrameHeadersSize;

        // see WriteFrameHeaders
        static (int, bool) ReadFrameHeaders(ReadOnlySpan<byte> input)
        {
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(input);
            return (chunkSize & int.MaxValue, chunkSize < 0);
        }
    }

    private void StartFrameRead()
    {
        BeginReadFrameHeader();

        // how much bytes we should read from the stream to parse the frame headers
        var frameHeaderRemainingBytes = FrameHeadersSize - bufferEnd;

        // frame header is not yet in the buffer
        if (frameHeaderRemainingBytes > 0)
            bufferEnd = ReadFromTransport(frameHeaderRemainingBytes, buffer.Span.Slice(bufferEnd));

        EndReadFrameHeader();
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
                StartFrameRead();
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

    private ValueTask StartFrameReadAsync(CancellationToken token)
    {
        BeginReadFrameHeader();

        // how much bytes we should read from the stream to parse the frame headers
        var frameHeaderRemainingBytes = FrameHeadersSize - bufferEnd;

        // frame header is not yet in the buffer
        if (frameHeaderRemainingBytes > 0)
            return StartFrameReadAsync(frameHeaderRemainingBytes, token);

        EndReadFrameHeader();
        return ValueTask.CompletedTask;
    }

    private async ValueTask StartFrameReadAsync(int frameHeaderRemainingBytes, CancellationToken token)
    {
        bufferEnd += await ReadFromTransportAsync(frameHeaderRemainingBytes, buffer.Memory.Slice(bufferEnd), token).ConfigureAwait(false);
        EndReadFrameHeader();
    }

    public sealed override async ValueTask<int> ReadAsync(Memory<byte> output, CancellationToken token)
    {
        while (true)
        {
            switch ((readState, frameSize))
            {
                case (ReadState.FrameStarted, 0):
                case (ReadState.FrameNotStarted, _):
                    await StartFrameReadAsync(token).ConfigureAwait(false);
                    continue; // skip empty frames
                case (ReadState.EndOfStreamReached, 0):
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
    }

    public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

    internal async ValueTask SkipAsync(CancellationToken token)
    {
        while (true)
        {
            switch ((readState, frameSize))
            {
                case (ReadState.FrameStarted, 0):
                case (ReadState.FrameNotStarted, _):
                    await StartFrameReadAsync(token).ConfigureAwait(false);
                    continue; // skip empty frames
                case (ReadState.EndOfStreamReached, 0):
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