using System.Threading.Tasks.Sources;

namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    internal sealed class YieldingEnumerable<T>(IEnumerable<T> enumerable) : IAsyncEnumerable<T>
    {
        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken token)
            => new YieldingEnumerator<T>(enumerable, token);
    }
    
    private sealed class YieldingEnumerator<T>(IEnumerable<T> enumerable, CancellationToken token) :
        IAsyncEnumerator<T>,
        IThreadPoolWorkItem,
        IValueTaskSource<bool>
    {
        private readonly IEnumerator<T> enumerator = enumerable.GetEnumerator();
        private ManualResetValueTaskSourceCore<bool> source = new() { RunContinuationsAsynchronously = false };

        T IAsyncEnumerator<T>.Current => enumerator.Current;

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
        {
            var version = source.Version;
            ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
            return new(this, version);
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (token.IsCancellationRequested)
            {
                source.SetException(new OperationCanceledException(token));
            }
            else
            {
                bool result;
                try
                {
                    result = enumerator.MoveNext();
                }
                catch (Exception e)
                {
                    source.SetException(e);
                    return;
                }

                source.SetResult(result);
            }
        }

        bool IValueTaskSource<bool>.GetResult(short version)
        {
            try
            {
                return source.GetResult(version);
            }
            finally
            {
                source.Reset();
            }
        }

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short version) => source.GetStatus(version);

        void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short version, ValueTaskSourceOnCompletedFlags flags)
            => source.OnCompleted(continuation, state, version, flags);
        
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            var task = ValueTask.CompletedTask;
            try
            {
                enumerator.Dispose();
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }

            return task;
        }
    }
}