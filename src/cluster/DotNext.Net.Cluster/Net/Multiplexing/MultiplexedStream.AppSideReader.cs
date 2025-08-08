using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class MultiplexedStream
{
    private sealed class AppSideReader(IApplicationSideStream state, PipeReader reader, AsyncAutoResetEvent writeSignal) : PipeReader
    {
        public override bool TryRead(out ReadResult result) => reader.TryRead(out result);

        public override ValueTask<ReadResult> ReadAsync(CancellationToken token = default)
            => reader.ReadAsync(token);

        public override void AdvanceTo(SequencePosition consumed)
            => reader.AdvanceTo(consumed);

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
            => reader.AdvanceTo(consumed, examined);

        public override void CancelPendingRead()
            => reader.CancelPendingRead();

        public override void Complete(Exception? exception = null)
        {
            if (state.TryCompleteOutput())
            {
                try
                {
                    reader.Complete(exception);
                }
                finally
                {
                    writeSignal.Set();
                }
            }
        }

        public override async ValueTask CompleteAsync(Exception? exception = null)
        {
            if (state.TryCompleteOutput())
            {
                try
                {
                    await reader.CompleteAsync(exception).ConfigureAwait(false);
                }
                finally
                {
                    writeSignal.Set();
                }
            }
        }
    }
}