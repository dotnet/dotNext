using System.Threading.Tasks.Sources;

namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    internal interface IProxyEnumerator<out TSelf, T> : IAsyncEnumerator<T>
        where TSelf : IProxyEnumerator<TSelf, T>
    {
        public static abstract TSelf Create(IEnumerable<T> enumerable, CancellationToken token);
    }
    
    internal abstract class ProxyEnumerator<T>(IEnumerable<T> enumerable, CancellationToken token) : IAsyncEnumerator<T>
    {
        protected readonly CancellationToken token = token;
        private readonly IEnumerator<T> enumerator = enumerable.GetEnumerator();
        
        protected bool MoveNext() => enumerator.MoveNext();

        public T Current => enumerator.Current;

        public abstract ValueTask<bool> MoveNextAsync();
        
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

    internal sealed class YieldingEnumerator<T>(IEnumerable<T> enumerable, CancellationToken token) : ProxyEnumerator<T>(enumerable, token),
        IValueTaskSource<bool>, IProxyEnumerator<YieldingEnumerator<T>, T>, IThreadPoolWorkItem
    {
        private ManualResetValueTaskSourceCore<bool> source = new() { RunContinuationsAsynchronously = false };

        public override ValueTask<bool> MoveNextAsync()
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
                    result = MoveNext();
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

        static YieldingEnumerator<T> IProxyEnumerator<YieldingEnumerator<T>, T>.Create(IEnumerable<T> enumerable, CancellationToken token)
            => new(enumerable, token);
    }

    internal sealed class Enumerator<T>(IEnumerable<T> enumerable, CancellationToken token)
        : ProxyEnumerator<T>(enumerable, token), IProxyEnumerator<Enumerator<T>, T>
    {
        public override ValueTask<bool> MoveNextAsync()
        {
            ValueTask<bool> task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled<bool>(token);
            }
            else
            {
                try
                {
                    task = new(MoveNext());
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<bool>(e);
                }
            }

            return task;
        }

        static Enumerator<T> IProxyEnumerator<Enumerator<T>, T>.Create(IEnumerable<T> enumerable, CancellationToken token)
            => new(enumerable, token);
    }

    internal sealed class Proxy<T, TEnumerator>(IEnumerable<T> enumerable) : IAsyncEnumerable<T>
        where TEnumerator : class, IProxyEnumerator<TEnumerator, T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
            => TEnumerator.Create(enumerable, token);
    }
}