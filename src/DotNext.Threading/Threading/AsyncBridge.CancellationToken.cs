using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Collections.Concurrent;
using Tasks;

public static partial class AsyncBridge
{
    private sealed class PoolingCancellationTokenValueTask : ValueTaskCompletionSource
    {
        private readonly Action<PoolingCancellationTokenValueTask> backToPool;
        internal new bool CompleteAsCanceled;

        internal PoolingCancellationTokenValueTask(Action<PoolingCancellationTokenValueTask> backToPool)
            => this.backToPool = backToPool;

        protected override void AfterConsumed()
        {
            if (!TryReset(out _))
            {
                // cannot be returned to the pool
            }
            else if (Interlocked.Increment(ref instantiatedTasks) > maxPoolSize)
            {
                Interlocked.Decrement(ref instantiatedTasks);
            }
            else
            {
                backToPool(this);
            }
        }

        protected override Exception? OnCanceled(CancellationToken token)
            => CompleteAsCanceled ? new OperationCanceledException(token) : null;
    }
    
    private abstract class CancellationTokenCompletionSource : TaskCompletionSource<CancellationToken>
    {
        protected static readonly Action<object?, CancellationToken> Callback = OnCanceled;

        private bool initialized; // volatile

        protected CancellationTokenCompletionSource(out InitializationFlag flag)
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
            => flag = new(ref initialized);

        private static void OnCanceled(object? source, CancellationToken token)
        {
            Debug.Assert(source is CancellationTokenCompletionSource);

            Unsafe.As<CancellationTokenCompletionSource>(source).OnCanceled(token);
        }

        private void OnCanceled(CancellationToken token)
        {
            if (Volatile.Read(ref initialized) && TrySetResult(token))
            {
                CleanUp();
            }
        }

        internal bool TryInterrupt(object? reason)
        {
            bool result;
            if (result = TrySetException(new PendingTaskInterruptedException { Reason = reason }))
            {
                CleanUp();
            }

            return result;
        }

        private static void Unregister(ReadOnlySpan<CancellationTokenRegistration> registrations)
        {
            foreach (ref readonly var registration in registrations)
            {
                registration.Unregister();
            }
        }

        private protected virtual void CleanUp() => Unregister(Registrations);
        
        private protected abstract ReadOnlySpan<CancellationTokenRegistration> Registrations { get; }

        [StructLayout(LayoutKind.Auto)]
        protected readonly ref struct InitializationFlag
        {
            private readonly ref bool flag;

            internal InitializationFlag(ref bool flag) => this.flag = ref flag;

            internal CancellationToken InitializationCompleted(ReadOnlySpan<CancellationTokenRegistration> registrations)
            {
                Volatile.Write(ref flag, true);

                foreach (ref readonly var registration in registrations)
                {
                    if (registration.Token.IsCancellationRequested)
                    {
                        return registration.Token;
                    }
                }

                return new(canceled: false);
            }
        }
    }
    
    private sealed class CancellationTokenCompletionSource1 : CancellationTokenCompletionSource
    {
        private readonly CancellationTokenRegistration registration;

        internal CancellationTokenCompletionSource1(CancellationToken token)
            : base(out var flag)
        {
            registration = token.UnsafeRegister(Callback, this);

            if (flag.InitializationCompleted(new(in registration)) is { IsCancellationRequested: true } canceledToken)
                Callback(this, canceledToken);
        }

        private protected override ReadOnlySpan<CancellationTokenRegistration> Registrations => new(in registration);
    }

    private sealed class CancellationTokenCompletionSource2 : CancellationTokenCompletionSource
    {
        private readonly (CancellationTokenRegistration, CancellationTokenRegistration) registrations;

        internal CancellationTokenCompletionSource2(CancellationToken token1, CancellationToken token2)
            : base(out var flag)
        {
            registrations.Item1 = token1.UnsafeRegister(Callback, this);
            registrations.Item2 = token2.UnsafeRegister(Callback, this);

            if (flag.InitializationCompleted(registrations.AsReadOnlySpan()) is { IsCancellationRequested: true } canceledToken)
                Callback(this, canceledToken);
        }

        private protected override ReadOnlySpan<CancellationTokenRegistration> Registrations => registrations.AsReadOnlySpan();
    }

    private sealed class CancellationTokenCompletionSourceN : CancellationTokenCompletionSource
    {
        private readonly CancellationTokenRegistration[] registrations;

        internal CancellationTokenCompletionSourceN(ReadOnlySpan<CancellationToken> tokens)
            : base(out var flag)
        {
            registrations = ArrayPool<CancellationTokenRegistration>.Shared.Rent(tokens.Length);

            for (var i = 0; i < tokens.Length; i++)
            {
                registrations[i] = tokens[i].UnsafeRegister(Callback, this);
            }

            if (flag.InitializationCompleted(registrations) is { IsCancellationRequested: true } canceledToken)
                Callback(this, canceledToken);
        }

        private protected override ReadOnlySpan<CancellationTokenRegistration> Registrations => new(registrations);

        private protected override void CleanUp()
        {
            ArrayPool<CancellationTokenRegistration>.Shared.Return(registrations, clearArray: true);
            base.CleanUp();
        }
    }

    private static readonly Action<PoolingCancellationTokenValueTask> CancellationTokenValueTaskCompletionCallback
        = new UnboundedObjectPool<PoolingCancellationTokenValueTask>().Return;

    private static UnboundedObjectPool<PoolingCancellationTokenValueTask> TokenPool
    {
        get
        {
            Debug.Assert(CancellationTokenValueTaskCompletionCallback.Target is UnboundedObjectPool<PoolingCancellationTokenValueTask>);

            return Unsafe.As<UnboundedObjectPool<PoolingCancellationTokenValueTask>>(CancellationTokenValueTaskCompletionCallback.Target);
        }
    }

    private sealed class TaskToCancellationTokenCallback
    {
        private volatile WeakReference<CancellationTokenSource>? sourceRef;

        internal TaskToCancellationTokenCallback(out CancellationToken token)
        {
            var source = new CancellationTokenSource();
            token = source.Token;
            sourceRef = new(source);
        }

        private bool TryStealSource([NotNullWhen(true)] out CancellationTokenSource? source)
        {
            source = null;
            return Interlocked.Exchange(ref sourceRef, null) is { } weakRef
                   && weakRef.TryGetTarget(out source);
        }

        internal bool TryDispose()
        {
            if (TryStealSource(out var source))
            {
                source.Dispose();
            }

            return source is not null;
        }

        internal void CancelAndDispose()
        {
            if (TryStealSource(out var source))
            {
                try
                {
                    source.Cancel();
                }
                finally
                {
                    source.Dispose();
                }
            }
        }
    }
}