using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    internal static class StreamExtensions
    {
        internal static async ValueTask CopyToAsync(this Stream source, PipeWriter output, bool resetStream, int bufferSize = 1024, CancellationToken token = default)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var buffer = new byte[bufferSize];
            int count;
            while ((count = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
            {
                var result = await output.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, count), token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
                result = await output.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
            }

            if (resetStream && source.CanSeek)
                source.Seek(0, SeekOrigin.Begin);
        }
    }
}
