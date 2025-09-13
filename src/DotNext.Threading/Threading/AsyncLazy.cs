﻿using System.Diagnostics;

namespace DotNext.Threading;

using Runtime;
using static Tasks.Synchronization;

/// <summary>
/// Provides support for asynchronous lazy initialization.
/// </summary>
/// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
[DebuggerDisplay($"IsValueCreated = {{{nameof(IsValueCreated)}}}")]
public class AsyncLazy<T> : ISupplier<CancellationToken, Task<T>>, IResettable
{
    private const string NotAvailable = "<NotAvailable>";
    private readonly BoxedValue<bool> resettable; // used as monitor
    private Task<T>? task;
    private Func<CancellationToken, Task<T>>? factory;

    /// <summary>
    /// Initializes a new instance of lazy value which is already computed.
    /// </summary>
    /// <param name="value">Already computed value.</param>
    public AsyncLazy(T value)
    {
        task = Task.FromResult(value);
        resettable = BoxedValue<bool>.Box(false);
    }

    /// <summary>
    /// Initializes a new instance of lazy value.
    /// </summary>
    /// <param name="valueFactory">The function used to compute actual value.</param>
    /// <param name="resettable"><see langword="true"/> if previously computed value can be removed and computation executed again when it will be requested; <see langword="false"/> if value can be computed exactly once.</param>
    /// <exception cref="ArgumentException"><paramref name="valueFactory"/> is <see langword="null"/>.</exception>
    public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory, bool resettable = false)
    {
        factory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        this.resettable = BoxedValue<bool>.Box(resettable);
    }

    private void AttachFactoryErasureCallback(Task expectedTask)
    {
        expectedTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(EraseFactory);

        void EraseFactory()
        {
            if (expectedTask is { IsCanceled: false } && ReferenceEquals(Volatile.Read(ref task), expectedTask))
            {
                lock (resettable)
                {
                    if (ReferenceEquals(task, expectedTask))
                        factory = null;
                }
            }
        }
    }

    /// <summary>
    /// Gets a value that indicates whether a value has been computed.
    /// </summary>
    public bool IsValueCreated => Volatile.Read(ref task) is { Status: TaskStatus.RanToCompletion or TaskStatus.Faulted };

    /// <summary>
    /// Gets value if it is already computed.
    /// </summary>
    public Result<T>? Value => Volatile.Read(ref task).TryGetResult();

    /// <inheritdoc />
    Task<T> ISupplier<CancellationToken, Task<T>>.Invoke(CancellationToken token)
        => WithCancellation(token);

    private Task<T> GetOrStartAsync(CancellationToken token)
    {
        Task<T>? t;
        bool fastExit;

        lock (resettable)
        {
            t = task; // read barrier is provided by monitor

            if (t is { IsCanceled: false })
            {
                fastExit = true;
            }
            else
            {
                Debug.Assert(factory is not null);

                task = t = Task.Run(CreateAsyncFunc(factory, token));
                fastExit = false;
            }
        }

        // post-processing of task out of the lock
        if (fastExit)
        {
            t = t.WaitAsync(token);
        }
        else if (!resettable)
        {
            AttachFactoryErasureCallback(t);
        }

        return t;

        // avoid capture of 'this' reference
        static Func<Task<T>> CreateAsyncFunc(Func<CancellationToken, Task<T>> cancelableFactory, CancellationToken token)
            => () => cancelableFactory(token);
    }

    /// <summary>
    /// Gets already completed task or invokes the factory.
    /// </summary>
    /// <remark>
    /// The canceled task will be restarted automatically even if the lazy container is not resettable.
    /// </remark>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Lazy representation of the value.</returns>
    public Task<T> WithCancellation(CancellationToken token)
        => Volatile.Read(ref task) is { IsCanceled: false } t ? t.WaitAsync(token) : GetOrStartAsync(token);

    /// <summary>
    /// Removes already computed value from the current object.
    /// </summary>
    /// <returns><see langword="true"/> if previous value is removed successfully; <see langword="false"/> if value is still computing or this instance is not resettable.</returns>
    public bool Reset()
    {
        bool result;
        if (result = resettable && Volatile.Read(ref task) is null or { IsCompleted: true })
        {
            lock (resettable)
            {
                if (result = task is null or { IsCompleted: true })
                    task = null;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    void IResettable.Reset() => Reset();

    /// <summary>
    /// Returns textual representation of this object.
    /// </summary>
    /// <returns>The string representing this object.</returns>
    public override string? ToString()
    {
        return Volatile.Read(ref task) is not { } t
            ? NotAvailable
            : t.Status is TaskStatus.RanToCompletion
            ? t.Result?.ToString()
            : $"<{t.Status}>";
    }
}