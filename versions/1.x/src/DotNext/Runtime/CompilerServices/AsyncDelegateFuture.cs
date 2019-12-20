using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    using static Threading.AtomicInt64;
    using static Threading.AtomicReference;

    /// <summary>
    /// Future representing asynchronous execution of multiple delegates.
    /// </summary>
    public abstract class AsyncDelegateFuture : Threading.Tasks.Future<Task>, Threading.Tasks.Future.IAwaiter
    {
        private protected readonly CancellationToken token;

        private protected AsyncDelegateFuture(CancellationToken token) => this.token = token;

        private protected abstract void ThrowIfNeeded();

        /// <summary>
        /// Retrieves awaiter.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public IAwaiter GetAwaiter() => this;

        void IAwaiter.GetResult()
        {
            if (IsCompleted)
                ThrowIfNeeded();
            else
                throw new IncompletedFutureException();
        }

        private async Task ExecuteAsTask() => await this;

        /// <summary>
        /// Converts cancellation token into <see cref="Task"/>.
        /// </summary>
        /// <returns>The task representing cancellation token.</returns>
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

        private protected override void ThrowIfNeeded() => throw new OperationCanceledException(token);
    }

    internal abstract class AsyncDelegateFuture<D> : AsyncDelegateFuture
        where D : MulticastDelegate
    {
        private const long CompletedState = -1L;
        private long index, totalCount;
        private volatile bool hasErrors;
        private volatile object exceptions; //has type Exception[] or AggregateException

        private protected AsyncDelegateFuture(CancellationToken token)
            : base(token)
        {
        }

        public sealed override bool IsCompleted => totalCount.VolatileRead() == CompletedState;

        private protected sealed override void ThrowIfNeeded()
        {
            if (exceptions is AggregateException error)
                throw error;
        }

        private protected abstract void InvokeOne(D d);

        private void InvokeOne(object d)
        {
            var errors = (Exception[])exceptions;
            var currentIndex = index.IncrementAndGet();
            if (d is D @delegate)
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        errors[currentIndex] = new OperationCanceledException(token);
                        hasErrors = true;
                    }
                    else
                        InvokeOne(@delegate);
                }
                catch (Exception e)
                {
                    hasErrors = true;
                    errors[currentIndex] = e;
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
            totalCount.VolatileWrite(CompletedState);
            Complete();
        }

        internal AsyncDelegateFuture<D> Invoke(D invocationList)
        {
            if (token.IsCancellationRequested)
                Complete(new[] { new OperationCanceledException(token) });
            else
            {
                var list = invocationList.GetInvocationList();
                index = -1L;
                totalCount = list.LongLength;
                exceptions = new Exception[list.LongLength];
                //TODO: Should be replaced with typed QueueUserWorkItem in .NET Standard 2.1
                var invoker = new WaitCallback(InvokeOne);
                foreach (D instance in list)
                    ThreadPool.QueueUserWorkItem(invoker, instance);
            }
            return this;
        }
    }

    internal sealed class CustomDelegateFuture<D> : AsyncDelegateFuture<D>
        where D : MulticastDelegate
    {
        private readonly Action<D> invoker;

        internal CustomDelegateFuture(Action<D> invoker, CancellationToken token) : base(token) => this.invoker = invoker;

        private protected override void InvokeOne(D d) => invoker(d);
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

    internal sealed class EventHandlerFuture<T> : AsyncDelegateFuture<EventHandler<T>>
    {
        private readonly object sender;
        private readonly T args;

        internal EventHandlerFuture(object sender, T args, CancellationToken token)
            : base(token)
        {
            this.sender = sender;
            this.args = args;
        }

        private protected override void InvokeOne(EventHandler<T> handler) => handler(sender, args);
    }

    internal sealed class ActionFuture : AsyncDelegateFuture<Action>
    {
        internal ActionFuture(CancellationToken token)
            : base(token)
        {
        }

        private protected override void InvokeOne(Action handler) => handler();
    }

    internal sealed class ActionFuture<T> : AsyncDelegateFuture<Action<T>>
    {
        private readonly T arg;

        internal ActionFuture(T arg, CancellationToken token) : base(token) => this.arg = arg;

        private protected override void InvokeOne(Action<T> handler) => handler(arg);
    }

    internal sealed class ActionFuture<T1, T2> : AsyncDelegateFuture<Action<T1, T2>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;

        internal ActionFuture(T1 arg1, T2 arg2, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
        }

        private protected override void InvokeOne(Action<T1, T2> handler) => handler(arg1, arg2);
    }

    internal sealed class ActionFuture<T1, T2, T3> : AsyncDelegateFuture<Action<T1, T2, T3>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
        }

        private protected override void InvokeOne(Action<T1, T2, T3> handler) => handler(arg1, arg2, arg3);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4> : AsyncDelegateFuture<Action<T1, T2, T3, T4>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4> handler) => handler(arg1, arg2, arg3, arg4);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4, T5> : AsyncDelegateFuture<Action<T1, T2, T3, T4, T5>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;
        private readonly T5 arg5;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4, T5> handler) => handler(arg1, arg2, arg3, arg4, arg5);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4, T5, T6> : AsyncDelegateFuture<Action<T1, T2, T3, T4, T5, T6>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;
        private readonly T5 arg5;
        private readonly T6 arg6;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
            this.arg6 = arg6;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4, T5, T6> handler) => handler(arg1, arg2, arg3, arg4, arg5, arg6);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4, T5, T6, T7> : AsyncDelegateFuture<Action<T1, T2, T3, T4, T5, T6, T7>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;
        private readonly T5 arg5;
        private readonly T6 arg6;
        private readonly T7 arg7;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
            this.arg6 = arg6;
            this.arg7 = arg7;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4, T5, T6, T7> handler) => handler(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4, T5, T6, T7, T8> : AsyncDelegateFuture<Action<T1, T2, T3, T4, T5, T6, T7, T8>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;
        private readonly T5 arg5;
        private readonly T6 arg6;
        private readonly T7 arg7;
        private readonly T8 arg8;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
            this.arg6 = arg6;
            this.arg7 = arg7;
            this.arg8 = arg8;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4, T5, T6, T7, T8> handler) => handler(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4, T5, T6, T7, T8, T9> : AsyncDelegateFuture<Action<T1, T2, T3, T4, T5, T6, T7, T8, T9>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;
        private readonly T5 arg5;
        private readonly T6 arg6;
        private readonly T7 arg7;
        private readonly T8 arg8;
        private readonly T9 arg9;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
            this.arg6 = arg6;
            this.arg7 = arg7;
            this.arg8 = arg8;
            this.arg9 = arg9;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> handler) => handler(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }

    internal sealed class ActionFuture<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : AsyncDelegateFuture<Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;
        private readonly T4 arg4;
        private readonly T5 arg5;
        private readonly T6 arg6;
        private readonly T7 arg7;
        private readonly T8 arg8;
        private readonly T9 arg9;
        private readonly T10 arg10;

        internal ActionFuture(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, CancellationToken token)
            : base(token)
        {
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
            this.arg6 = arg6;
            this.arg7 = arg7;
            this.arg8 = arg8;
            this.arg9 = arg9;
            this.arg10 = arg10;
        }

        private protected override void InvokeOne(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> handler) => handler(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
}