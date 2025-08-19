using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

using Buffers;

partial class MultiplexedStream
{
    // this is the maximum number of bytes that we can send without adjusting the window
    private volatile int inputWindow;

    public ValueTask WriteFrameAsync(IBufferWriter<byte> writer, uint streamId)
    {
        ValueTask task;
        if (!IsTransportInputCompleted(out var appSideCompleted))
        {
            bool resultTaken;
            ReadResult result;
            try
            {
                resultTaken = transportReader.TryRead(out result);
            }
            catch (Exception e)
            {
                task = CompleteTransportInputAsync(e);
                transportSignal.Set(); // pass to the branch below (when application-side of the pipe is completed) on the next iteration
                goto exit;
            }

            if (!resultTaken)
            {
                AdjustWindowIfNeeded(writer, streamId);
            }
            else if (result.IsCanceled)
            {
                task = CompleteTransportInputAsync();
                goto exit;
            }
            else
            {
                task = WriteFrameAsync(writer, streamId, result);
                goto exit;
            }
        }
        else if (appSideCompleted)
        {
            task = CloseAndCancelOutputAsync();
            goto exit;
        }

        task = ValueTask.CompletedTask;

        exit:
        return task;
    }

    private ValueTask CloseAndCancelOutputAsync()
    {
        transportWriter.CancelPendingFlush();
        var task = CompleteTransportOutputAsync();
        transportSignal.Set();
        return task;
    }

    private ValueTask WriteFrameAsync(IBufferWriter<byte> writer, uint streamId, in ReadResult result)
    {
        AdjustWindowIfNeeded(writer, streamId);
        return WriteFrame(writer, streamId, result)
            ? CompleteTransportInputAsync()
            : ValueTask.CompletedTask;
    }

    private bool WriteFrame(Span<byte> frameBuffer, uint streamId, in ReadResult readResult, out int bytesWritten, out SequencePosition consumed)
    {
        Debug.Assert(frameBuffer.Length > FrameHeader.Size);

        var inputBuffer = readResult.Buffer;
        bool completed;
        // we cannot send more than window size
        if (frameBuffer.Slice(FrameHeader.Size).TrimLength(inputWindow) is { Length: > 0 } payload)
        {
            bytesWritten = inputBuffer.CopyTo(payload, out consumed);

            Debug.Assert(bytesWritten <= MaxFrameSize);

            FrameControl control;
            if (!consumed.Equals(inputBuffer.End))
            {
                control = FrameControl.DataChunk;
                completed = false;
                transportSignal.Set();
            }
            else if (completed = readResult.IsCompleted)
            {
                control = FrameControl.FinalDataChunk;
            }
            else
            {
                control = FrameControl.DataChunk;
                transportSignal.Set();
            }

            Interlocked.Add(ref inputWindow, -bytesWritten);
            new FrameHeader(streamId, control, (ushort)bytesWritten).Format(frameBuffer);
            bytesWritten += FrameHeader.Size;
        }
        else
        {
            // input window is empty, nothing we can read
            completed = false;
            bytesWritten = 0;
            consumed = inputBuffer.Start;
        }

        return completed;
    }

    private bool WriteFrame(IBufferWriter<byte> writer, uint streamId, in ReadResult result)
    {
        var buffer = writer.GetSpan(frameAndHeaderSize);
        var completed = WriteFrame(
            buffer.Slice(0, frameAndHeaderSize),
            streamId,
            result,
            out var bytesWritten,
            out var consumed);
        
        writer.Advance(bytesWritten);
        transportReader.AdvanceTo(consumed);

        return completed;
    }

    private void AdjustWindow(int length)
    {
        Interlocked.Add(ref inputWindow, length);
        transportSignal.Set();
    }
}