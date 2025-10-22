using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.IO;

partial class WriterStream<TOutput> : IValueTaskSource
{
    private ManualResetValueTaskSourceCore<int> source;
    private int chunkBytesWritten;
    private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter;
    private Action? endWriteCallback;

    private protected ValueTask SubmitWrite(int bytesWritten, ValueTask task)
    {
        chunkBytesWritten = bytesWritten;
        awaiter = task.ConfigureAwait(false).GetAwaiter();
        if (awaiter.IsCompleted)
        {
            EndWrite();
        }
        else
        {
            awaiter.UnsafeOnCompleted(endWriteCallback ??= EndWrite);
        }

        return new(this, source.Version);
    }

    private void EndWrite()
    {
        try
        {
            awaiter.GetResult();
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }
        finally
        {
            awaiter = default;
        }

        source.SetResult(chunkBytesWritten);
        writtenBytes += chunkBytesWritten;
    }

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => source.GetStatus(token);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => source.OnCompleted(continuation, state, token, flags);
}