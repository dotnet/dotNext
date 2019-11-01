using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    public static class StreamExtensions
    {
        public static async ValueTask<long> ReadAsync(this Stream source, PipeWriter destination, int bufferSize = 0, CancellationToken token = default)
        {
            var total = 0L;
            for (int bytesRead; ; token.ThrowIfCancellationRequested())
            {
                bytesRead = await source.ReadAsync(destination.GetMemory(bufferSize), token).ConfigureAwait(false);
                destination.Advance(bytesRead);
                if (bytesRead == 0)
                    break;
                total += bytesRead;
                var result = await destination.FlushAsync().ConfigureAwait(false);
                if (result.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                if (result.IsCompleted)
                    break;
            }
            return total;
        }

        public static async ValueTask<long> WriteAsync(this Stream destination, PipeReader source, CancellationToken token = default)
        {
            var total = 0L;
            for (ReadResult result; ; token.ThrowIfCancellationRequested())
            {
                result = await source.ReadAsync(token).ConfigureAwait(false);
                if (result.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                if (result.IsCompleted)
                    break;
                total += result.Buffer.Length;
                foreach (var block in result.Buffer)
                    await destination.WriteAsync(block, token).ConfigureAwait(false);
            }
            return total;
        }
    }
}
