using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Future representing asynchronous execution of multiple delegates.
    /// </summary>
    public abstract class AsyncDelegateFuture : Tasks.Future<Task>
    {
        public readonly struct Awaiter : INotifyCompletion
        {
            private readonly AsyncDelegateFuture future;

            internal Awaiter(AsyncDelegateFuture future) => this.future = future;

            public bool IsCompleted => future is null || future.IsCompleted;

            public void GetResult()
            {
                if(future is null)
                    return;
                else if(future.IsCompleted)
                    future.ThrowIfNeeded();
                else
                    throw new InvalidOperationException();
            }

            public void OnCompleted(Action callback)
            {
                if(IsCompleted)
                    callback();
                else
                    future.OnCompleted(callback);
            }
        }

        private protected CancellationToken token;

        private protected AsyncDelegateFuture(CancellationToken token) => this.token = token;

        private protected abstract void ThrowIfNeeded();

        public Awaiter GetAwaiter() => new Awaiter(this);

        private async Task ExecuteAsTask() => await this;

        public sealed override Task AsTask() => token.IsCancellationRequested ? Task.FromCanceled(token) : ExecuteAsTask();
    }

    internal sealed class CanceledAsyncDelegateFuture : AsyncDelegateFuture
    {
        internal static readonly AsyncDelegateFuture Instance = new CanceledAsyncDelegateFuture();

        private CanceledAsyncDelegateFuture()
            : base(new CancellationToken(true))
        {
        }

        public sealed override bool IsCompleted => true;

        private protected override void ThrowIfNeeded() => new OperationCanceledException(token);
    }

    internal abstract class AsyncDelegateFuture<D> : AsyncDelegateFuture
        where D : MulticastDelegate
    {
        private long index;
        private long totalCount;
        private volatile bool hasErrors;
        private volatile object exceptions; //has type Exception[] or AggregateException

        private protected AsyncDelegateFuture(CancellationToken token)
            : base(token)
        {
        }

        public sealed override bool IsCompleted => totalCount.VolatileRead() == 0L || token.IsCancellationRequested;

        private protected sealed override void ThrowIfNeeded()
        {
            if (exceptions is AggregateException error)
                throw error;
        }

        private protected abstract void InvokeOne(D d);

        private void InvokeOne(object d)
        {
            var errors = (Exception[]) exceptions;
            var currentIndex = index.IncrementAndGet();
            if(d is D @delegate)
                try
                {
                    if(token.IsCancellationRequested)
                    {
                        errors.VolatileWrite(currentIndex, new OperationCanceledException(token));
                        hasErrors = true;
                    }
                    else
                        InvokeOne(@delegate);
                }
                catch(Exception e)
                {
                    hasErrors = true;
                    errors.VolatileWrite(currentIndex, e);
                }
                finally
                {
                    if (totalCount.DecrementAndGet() == 0)
                        Complete(hasErrors ? errors : null);
                }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Complete(Exception[] errors)
        {
            if (errors != null)
                exceptions = new AggregateException(errors.SkipNulls());
            Complete();
        }

        internal AsyncDelegateFuture<D> Invoke(D invocationList)
        {
            if(token.IsCancellationRequested)
            {
                exceptions = new Exception[] { new OperationCanceledException(token) };
                Complete();
            }
            else
            {
                var list = invocationList.GetInvocationList();
                index = -1L;
                totalCount = list.LongLength;
                exceptions = new Exception[list.LongLength];
                var invoker = new WaitCallback(InvokeOne);
                foreach(D instance in list)
                    ThreadPool.QueueUserWorkItem(invoker, instance);
            }
            return this;
        }
    }

    internal sealed class EventHandlerFuture : AsyncDelegateFuture<EventHandler>
    {
        private readonly object sender;
        private readonly EventArgs args;

        internal EventHandlerFuture(object sender, EventArgs args, CancellationToken token)
            : base(token)
        {
            this.sender = sender;
            this.args = args;
        }

        private protected override void InvokeOne(EventHandler handler) => handler(sender, args);
    }

    internal sealed class ActionFuture : AsyncDelegateFuture<Action>
    {
        internal ActionFuture(CancellationToken token)
            : base(token)
        {
        }

        private protected override void InvokeOne(Action handler) => handler();
    }
}