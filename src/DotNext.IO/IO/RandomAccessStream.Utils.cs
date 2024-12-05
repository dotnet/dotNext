using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.IO;

public partial class RandomAccessStream : IValueTaskSource, IValueTaskSource<int>
{
    private ManualResetValueTaskSourceCore<int> source;
    private int bytesWritten;
    private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter writeTask;
    private ConfiguredValueTaskAwaitable<int>.ConfiguredValueTaskAwaiter readTask;
    private Action? readCallback, writeCallback;

    internal ValueTask SubmitWrite(ValueTask writeTask, int bytesWritten)
    {
        this.bytesWritten = bytesWritten;
        this.writeTask = writeTask.ConfigureAwait(false).GetAwaiter();
        if (this.writeTask.IsCompleted)
        {
            OnWriteCompleted();
        }
        else
        {
            this.writeTask.UnsafeOnCompleted(writeCallback ??= OnWriteCompleted);
        }

        return new(this, source.Version);
    }

    internal ValueTask<int> SubmitRead(ValueTask<int> readTask)
    {
        this.readTask = readTask.ConfigureAwait(false).GetAwaiter();
        if (this.readTask.IsCompleted)
        {
            OnReadCompleted();
        }
        else
        {
            this.readTask.UnsafeOnCompleted(readCallback ??= OnReadCompleted);
        }

        return new(this, source.Version);
    }

    private void OnWriteCompleted()
    {
        var awaiter = writeTask;
        writeTask = default;

        try
        {
            awaiter.GetResult();
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(bytesWritten);
    }

    private void OnReadCompleted()
    {
        var awaiter = readTask;
        readTask = default;

        int bytesRead;
        try
        {
            bytesRead = awaiter.GetResult();
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(bytesRead);
    }

    public ValueTaskSourceStatus GetStatus(short token) => source.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => source.OnCompleted(continuation, state, token, flags);

    // write operation
    void IValueTaskSource.GetResult(short token)
    {
        int bytesWritten;
        try
        {
            bytesWritten = source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }

        Advance(bytesWritten);
    }

    // read operation
    int IValueTaskSource<int>.GetResult(short token)
    {
        int bytesRead;
        try
        {
            bytesRead = source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }

        Advance(bytesRead);
        return bytesRead;
    }
}