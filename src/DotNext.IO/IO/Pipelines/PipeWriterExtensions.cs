using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Text;

    public static class PipeWriterExtensions
    {
        public static async ValueTask WriteStringAsync(this PipeWriter writer, ReadOnlyMemory<char> value, EncodingContext context, int bufferSize = 0, CancellationToken token = default)
        {
            if (value.Length == 0)
                return;
            var encoder = context.GetEncoder();
            var completed = false;
            for (int offset = 0, charsUsed; !completed; offset += charsUsed)
            {
                var buffer = writer.GetMemory(bufferSize);
                var chars = value.Slice(offset);
                encoder.Convert(chars.Span, buffer.Span, chars.Length == 0, out charsUsed, out var bytesUsed, out completed);
                writer.Advance(bytesUsed);
                value = chars;
                var result = await writer.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
            }
        }
    }
}
