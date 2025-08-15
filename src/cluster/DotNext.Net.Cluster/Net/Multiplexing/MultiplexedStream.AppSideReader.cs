using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedStream
{
    private sealed class AppSideReader(IApplicationSideStream state, PipeReader reader) : PipeReader, IValueTaskSource<ReadResult>
    {
        private ManualResetValueTaskSourceCore<ReadResult> source = new() { RunContinuationsAsynchronously = true };
        private ConfiguredValueTaskAwaitable<ReadResult>.ConfiguredValueTaskAwaiter readAwaiter;
        private Action? readCallback;
        private ReadOnlySequence<byte> readBuffer;

        private void EndRead()
        {
            try
            {
                var result = readAwaiter.GetResult();
                readBuffer = result.Buffer;
                source.SetResult(result);
            }
            catch (Exception e)
            {
                source.SetException(e);
            }
            finally
            {
                readAwaiter = default;
            }
        }

        ReadResult IValueTaskSource<ReadResult>.GetResult(short token)
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

        ValueTaskSourceStatus IValueTaskSource<ReadResult>.GetStatus(short token) => source.GetStatus(token);

        void IValueTaskSource<ReadResult>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => source.OnCompleted(continuation, state, token, flags);

        public override bool TryRead(out ReadResult result)
        {
            bool resultTaken;
            if (resultTaken = reader.TryRead(out result))
            {
                readBuffer = result.Buffer;
            }

            return resultTaken;
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken token = default)
        {
            readAwaiter = reader.ReadAsync(token).ConfigureAwait(false).GetAwaiter();
            if (readAwaiter.IsCompleted)
            {
                EndRead();
            }
            else
            {
                readAwaiter.UnsafeOnCompleted(readCallback ??= EndRead);
            }

            return new(this, source.Version);
        }

        private void Consume(SequencePosition consumed)
        {
            state.Consume(readBuffer.Slice(readBuffer.Start, consumed).Length);
            readBuffer = default;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            Consume(consumed);
            reader.AdvanceTo(consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            Consume(consumed);
            reader.AdvanceTo(consumed, examined);
        }

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
                    state.TransportSignal.Set();
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
                    state.TransportSignal.Set();
                }
            }
        }
    }
}