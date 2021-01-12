using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Buffers
{
    internal static partial class BufferReader
    {
        internal static ValueTask Invoke<T, TArg>(this ReadOnlySpanAction<T, TArg> callback, TArg arg, ReadOnlyMemory<T> buffer, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    callback(buffer.Span, arg);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }
    }
}