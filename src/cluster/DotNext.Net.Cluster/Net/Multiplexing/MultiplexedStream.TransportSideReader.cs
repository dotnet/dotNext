using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedStream
{
    // this is the maximum number of bytes that we can send without adjusting the window
    private volatile int inputWindow;

    public ValueTask WriteFrameAsync(IBufferWriter<byte> writer, ulong streamId)
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

    private ValueTask WriteFrameAsync(IBufferWriter<byte> writer, ulong streamId, in ReadResult result)
    {
        AdjustWindowIfNeeded(writer, streamId);
        return WriteFrame(writer, streamId, result)
            ? CompleteTransportInputAsync()
            : ValueTask.CompletedTask;
    }

    private (int, SequencePosition) WriteFrame(Span<byte> output, ulong streamId, in ReadResult readResult, out bool completed)
    {
        Debug.Assert(output.Length > FrameHeader.Size);

        var inputBuffer = readResult.Buffer;
        int bytesWritten;
        SequencePosition position;
        if (output.Slice(FrameHeader.Size).TrimLength(inputWindow) is { Length: > 0 } payload)
        {
            // we cannot send more than window size
            bytesWritten = CopyTo(inputBuffer, payload, out position);

            Debug.Assert(bytesWritten <= MaxFrameSize);

            FrameControl control;
            if (!position.Equals(inputBuffer.End))
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
            new FrameHeader(streamId, control, (ushort)bytesWritten).Format(output);
            bytesWritten += FrameHeader.Size;
        }
        else
        {
            // input window is empty, nothing we can read
            completed = false;
            bytesWritten = 0;
            position = inputBuffer.Start;
        }

        return (bytesWritten, position);
        
        static int CopyTo(in ReadOnlySequence<byte> source, Span<byte> destination, out SequencePosition consumed)
        {
            var result = 0;

            consumed = source.Start;
            for (var enumerator = source.GetEnumerator(); !destination.IsEmpty && enumerator.MoveNext();)
            {
                var block = enumerator.Current;
                block.Span.CopyTo(destination, out var subcount);
                result += subcount;
                consumed = source.GetPosition(subcount, consumed);
                destination = destination.Slice(subcount);
            }

            return result;
        }
    }

    private bool WriteFrame(IBufferWriter<byte> writer, ulong streamId, in ReadResult result)
    {
        var buffer = writer.GetSpan(frameAndHeaderSize);
        var (bytesWritten, position) = WriteFrame(buffer.Slice(0, frameAndHeaderSize), streamId, result, out var completed);
        writer.Advance(bytesWritten);
        transportReader.AdvanceTo(position);
        
        return completed;
    }

    private void AdjustWindow(int length)
    {
        Interlocked.Add(ref inputWindow, length);
        transportSignal.Set();
    }
}