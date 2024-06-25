using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Tasks;

public static partial class AsyncBridge
{
    private sealed class CancellationTokenValueTask : ValueTaskCompletionSource
    {
        private readonly Action<CancellationTokenValueTask> backToPool;

        internal new bool CompleteAsCanceled;

        internal CancellationTokenValueTask(Action<CancellationTokenValueTask> backToPool)
        {
            this.backToPool = backToPool;
            Interlocked.Increment(ref instantiatedTasks);
        }

        protected override void AfterConsumed()
        {
            Interlocked.Decrement(ref instantiatedTasks);
            backToPool(this);
        }

        protected override Exception? OnCanceled(CancellationToken token)
            => CompleteAsCanceled ? new OperationCanceledException(token) : null;
    }

    private sealed class CancellationTokenValueTaskPool : ConcurrentBag<CancellationTokenValueTask>
    {
        internal void Return(CancellationTokenValueTask vt)
        {
            if (vt.TryReset(out _))
                Add(vt);
        }
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
                Cleanup();
            }
        }

        private static void Unregister(ReadOnlySpan<CancellationTokenRegistration> registrations)
        {
            foreach (ref readonly var registration in registrations)
            {
                registration.Unregister();
            }
        }

        private protected virtual void Cleanup() => Unregister(Registrations);
        
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

        private protected override void Cleanup()
        {
            ArrayPool<CancellationTokenRegistration>.Shared.Return(registrations, clearArray: true);
            base.Cleanup();
        }
    }

    private static readonly Action<CancellationTokenValueTask> CancellationTokenValueTaskCompletionCallback = new CancellationTokenValueTaskPool().Return;

    private static CancellationTokenValueTaskPool TokenPool
    {
        get
        {
            Debug.Assert(CancellationTokenValueTaskCompletionCallback.Target is CancellationTokenValueTaskPool);

            return Unsafe.As<CancellationTokenValueTaskPool>(CancellationTokenValueTaskCompletionCallback.Target);
        }
    }
}