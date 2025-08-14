using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class MultiplexedStream
{
    private readonly long resumeThreshold;
    private long bytesReceived;

    public ValueTask ReadFrameAsync(FrameControl control, ReadOnlyMemory<byte> payload, CancellationToken token)
        => IsTransportOutputCompleted ? ValueTask.CompletedTask : ReadFrameCoreAsync(control, payload, token);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ReadFrameCoreAsync(FrameControl control, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        FlushResult result;
        try
        {
            result = await ApplyFrameAsync(control, payload, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // on exception, complete input/output and remove the stream
            await CompleteTransportOutputAsync(e).ConfigureAwait(false);
            result = new(isCanceled: true, isCompleted: false);
        }

        if (!result.IsCanceled && (result.IsCompleted || control is FrameControl.FinalDataChunk))
        {
            await CompleteTransportOutputAsync().ConfigureAwait(false);
        }
    }

    private ValueTask<FlushResult> ApplyFrameAsync(FrameControl control, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        ValueTask<FlushResult> task;
        switch (control)
        {
            case FrameControl.StreamRejected:
                task = ValueTask.FromException<FlushResult>(new StreamRejectedException());
                break;
            case FrameControl.StreamClosed:
                task = CloseOutputAndCancelInputAsync();
                break;
            case FrameControl.AdjustWindow:
                AdjustWindow(Protocol.ReadAdjustWindow(payload.Span));
                task = ValueTask.FromResult<FlushResult>(new());
                break;
            case FrameControl.Heartbeat:
                Debug.Fail("Unexpected message type");
                goto default;
            default:
                task = transportWriter.WriteAsync(payload, token);
                break;
        }

        return task;
    }

    private async ValueTask<FlushResult> CloseOutputAndCancelInputAsync()
    {
        await CompleteTransportOutputAsync().ConfigureAwait(false);
        transportReader.CancelPendingRead();
        transportSignal.Set();
        return new(isCanceled: false, isCompleted: true);
    }

    void IApplicationSideStream.Consume(long count)
    {
        var totalReceived = Interlocked.Add(ref bytesReceived, count);
        if (totalReceived > resumeThreshold)
            transportSignal.Set();
    }

    private void AdjustWindowIfNeeded(IBufferWriter<byte> writer, ulong streamId)
    {
        var totalReceived = Atomic.Read(in bytesReceived);
        if (totalReceived > resumeThreshold)
        {
            var windowSize = int.CreateSaturating(totalReceived);
            Interlocked.Add(ref bytesReceived, -windowSize);
            Protocol.WriteAdjustWindow(writer, streamId, windowSize);
        }
    }
}