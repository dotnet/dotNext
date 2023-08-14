using System.Buffers.Binary;
using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;

internal partial class ProtocolStream
{
    internal SpanWriter<byte> BeginRequestMessage(MessageType type)
    {
        Reset();

        var writer = new SpanWriter<byte>(buffer.Span);
        writer.Write((byte)type);
        return writer;
    }

    internal void StartFrameWrite()
    {
        bufferStart = bufferEnd;
        bufferEnd = bufferStart + FrameHeadersSize;
    }

    internal void WriteFinalFrame()
    {
        WriteFrameHeaders(bufferEnd - bufferStart - FrameHeadersSize, finalBlock: true);
        bufferStart = bufferEnd;
    }

    internal void AdvanceWriteCursor(int count)
        => bufferEnd += count;

    internal bool CanWriteFrameSynchronously(int frameSize)
        => buffer.Length - bufferEnd >= frameSize + FrameHeadersSize;

    // highest bit in a frame header indicates final block
    private void WriteFrameHeaders(int chunkSize, bool finalBlock)
    {
        Debug.Assert(chunkSize >= 0);

        if (finalBlock)
            chunkSize |= int.MinValue;

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Span.Slice(bufferStart), chunkSize);
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

    internal ValueTask WriteToTransportAsync(CancellationToken token)
    {
        var bufferToPass = buffer.Memory.Slice(0, bufferEnd);
        bufferStart = bufferEnd = 0;
        return WriteToTransportAsync(bufferToPass, token);
    }

    // don't use this method internally to avoid allocation of Task, use WriteToTransportAsync instead
    public sealed override Task FlushAsync(CancellationToken token)
        => bufferEnd > 0 ? WriteToTransportAsync(token).AsTask() : Task.CompletedTask;

    public sealed override void Flush()
    {
        var bufferToPass = buffer.Span.Slice(0, bufferEnd);
        bufferStart = bufferEnd = 0;
        WriteToTransport(bufferToPass);
    }
}