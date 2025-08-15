using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedStream
{
    private sealed class AppSideWriter(IApplicationSideStream appSide, PipeWriter writer) : PipeWriter
    {
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
                    appSide.TransportSignal.Set();
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
                    appSide.TransportSignal.Set();
                }
            }
        }

        public override void CancelPendingFlush() => writer.CancelPendingFlush();
        
        public override bool CanGetUnflushedBytes => writer.CanGetUnflushedBytes;

        public override long UnflushedBytes => writer.UnflushedBytes;

        public override ValueTask<FlushResult> FlushAsync(CancellationToken token = default)
        {
            var task = writer.FlushAsync(token);
            appSide.TransportSignal.Set();
            return task;
        }

        public override void Advance(int bytes) => writer.Advance(bytes);

        public override Memory<byte> GetMemory(int sizeHint = 0) => writer.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => writer.GetSpan(sizeHint);
    }
}