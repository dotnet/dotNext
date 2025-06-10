using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using DotNext.Buffers;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed class StreamHandler(PipeOptions options, AsyncAutoResetEvent writeSignal) : PipeWriter, IDuplexPipe, IValueTaskSource<FlushResult>
{
    private readonly Pipe input = new(options), output = new(options);
    private ManualResetValueTaskSourceCore<FlushResult> source = new() { RunContinuationsAsynchronously = true };
    private ConfiguredValueTaskAwaitable<FlushResult>.ConfiguredValueTaskAwaiter flushAwaiter;
    private Action? flushCallback;

    private void EndFlush()
    {
        try
        {
            source.SetResult(flushAwaiter.GetResult());
        }
        catch (Exception e)
        {
            source.SetException(e);
        }
        finally
        {
            flushAwaiter = default;
        }

        writeSignal.Set();
    }

    ValueTaskSourceStatus IValueTaskSource<FlushResult>.GetStatus(short token) => source.GetStatus(token);

    FlushResult IValueTaskSource<FlushResult>.GetResult(short token)
    {
        try
        {
            return source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }
    }

    void IValueTaskSource<FlushResult>.OnCompleted(Action<object?> continuation, object? state, short token,
        ValueTaskSourceOnCompletedFlags flags)
        => source.OnCompleted(continuation, state, token, flags);

    public bool IsClosed { get; private set; }

    public void Close()
    {
        IsClosed = true;
        writeSignal.Set();
    }

    public PipeReader Input => input.Reader;

    public PipeWriter Output => output.Writer;

    PipeReader IDuplexPipe.Input => output.Reader;

    PipeWriter IDuplexPipe.Output => this;

    public override void Complete(Exception? exception = null)
    {
        writeSignal.Set();
        input.Writer.Complete(exception);
    }

    public override bool CanGetUnflushedBytes => input.Writer.CanGetUnflushedBytes;

    public override long UnflushedBytes => input.Writer.UnflushedBytes;

    public override async ValueTask CompleteAsync(Exception? exception = null)
    {
        try
        {
            await base.CompleteAsync(exception).ConfigureAwait(false);
        }
        finally
        {
            writeSignal.Set();
        }
    }

    public override void CancelPendingFlush() => input.Writer.CancelPendingFlush();

    public override ValueTask<FlushResult> FlushAsync(CancellationToken token = default)
    {
        flushAwaiter = input.Writer.FlushAsync(token).ConfigureAwait(false).GetAwaiter();
        if (flushAwaiter.IsCompleted)
        {
            EndFlush();
        }
        else
        {
            flushAwaiter.UnsafeOnCompleted(flushCallback ??= EndFlush);
        }

        return new(this, source.Version);
    }

    public override void Advance(int bytes) => input.Writer.Advance(bytes);

    public override Memory<byte> GetMemory(int sizeHint = 0) => input.Writer.GetMemory(sizeHint);

    public override Span<byte> GetSpan(int sizeHint = 0) => input.Writer.GetSpan(sizeHint);

    internal static async ValueTask CompleteAllAsync(ConcurrentDictionary<ulong, StreamHandler> streams, List<ulong> streamsToRemove, Exception e)
    {
        streamsToRemove.AddRange(streams.Keys);

        foreach (var id in streamsToRemove)
        {
            if (streams.TryRemove(id, out var stream))
            {
                await stream.Output.CompleteAsync(e).ConfigureAwait(false);
                await stream.Input.CompleteAsync(e).ConfigureAwait(false);
            }
        }
    }

    internal static ReadOnlyMemory<byte> PrepareFragment(Memory<byte> sendBuffer, AsyncAutoResetEvent writeSignal, ulong streamId, PipeReader reader,
        ReadOnlySequence<byte> inputBuffer, ref bool isCompleted)
    {
        inputBuffer.CopyTo(sendBuffer.Span.Slice(FragmentHeader.Size), out var writtenCount);
        var position = inputBuffer.GetPosition(writtenCount);
        reader.AdvanceTo(position);

        FragmentControl control;
        if (!position.Equals(inputBuffer.End))
        {
            writeSignal.Set();
            control = FragmentControl.DataChunk;
            isCompleted = false;
        }
        else if (isCompleted)
        {
            control = FragmentControl.FinalDataChunk;
        }
        else
        {
            control = FragmentControl.DataChunk;
        }

        new FragmentHeader(streamId, control, (ushort)writtenCount).Format(sendBuffer.Span);
        return sendBuffer.Slice(0, writtenCount + FragmentHeader.Size);
    }
}