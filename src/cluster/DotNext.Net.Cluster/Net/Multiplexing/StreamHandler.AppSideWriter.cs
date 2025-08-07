using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class MultiplexedStream
{
    private sealed class AppSideWriter(IApplicationSideStream appSide, PipeWriter writer, AsyncAutoResetEvent writeSignal) : PipeWriter, IValueTaskSource<FlushResult>
    {
        private ManualResetValueTaskSourceCore<FlushResult> source = new() { RunContinuationsAsynchronously = true };
        private ConfiguredValueTaskAwaitable<FlushResult>.ConfiguredValueTaskAwaiter flushAwaiter;
        private Action? flushCallback;

        public override async ValueTask CompleteAsync(Exception? exception = null)
        {
            if (appSide.TryCompleteInput())
            {
                try
                {
                    await writer.CompleteAsync(exception).ConfigureAwait(false);
                }
                finally
                {
                    writeSignal.Set();
                }
            }
        }

        public override void Complete(Exception? exception = null)
        {
            if (appSide.TryCompleteOutput())
            {
                try
                {
                    writer.Complete(exception);
                }
                finally
                {
                    writeSignal.Set();
                }
            }
        }

        public override void CancelPendingFlush() => writer.CancelPendingFlush();
        
        public override bool CanGetUnflushedBytes => writer.CanGetUnflushedBytes;

        public override long UnflushedBytes => writer.UnflushedBytes;

        public override ValueTask<FlushResult> FlushAsync(CancellationToken token = default)
        {
            flushAwaiter = writer.FlushAsync(token).ConfigureAwait(false).GetAwaiter();
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

        public override void Advance(int bytes) => writer.Advance(bytes);

        public override Memory<byte> GetMemory(int sizeHint = 0) => writer.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => writer.GetSpan(sizeHint);

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
    }
}