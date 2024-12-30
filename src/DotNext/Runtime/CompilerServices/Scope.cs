using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime.CompilerServices;

using ExceptionAggregator = ExceptionServices.ExceptionAggregator;

/// <summary>
/// Represents a collection of callbacks to be executed at the end of the lexical scope.
/// </summary>
/// <remarks>
/// This type allows to avoid usage of try-finally blocks within the code. It is suitable
/// for asynchronous and synchronous scenarios. However, you should not pass an instance of this type
/// as an argument to or return it from the method.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public struct Scope : IDisposable, IAsyncDisposable
{
    private sealed class DynamicTuple : List<object?>, ITuple
    {
        int ITuple.Length => Count;
    }

    // null, or Action, or Func<ValueTask>, or IDisposable, or IAsyncDisposable
    private (object? Callback0, object? Callback1, object? Callback2, object? Callback3) callbacks;
    private DynamicTuple? rest;

    private void Add(object callback)
    {
        Debug.Assert(callback is Action or Func<ValueTask> or IDisposable or IAsyncDisposable);

        if (rest is null)
        {
            foreach (ref var slot in callbacks.AsSpan())
            {
                if (slot is null)
                {
                    slot = callback;
                    return;
                }
            }

            rest = new();
        }

        rest.Add(callback);
    }

    /// <summary>
    /// Attaches callback to this lexical scope.
    /// </summary>
    /// <param name="callback">The callback to be attached to the current scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    public void Defer(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Add(callback);
    }

    /// <summary>
    /// Attaches callback to this lexical scope.
    /// </summary>
    /// <param name="callback">The callback to be attached to the current scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    public void Defer(Func<ValueTask> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Add(callback);
    }

    /// <summary>
    /// Registers an object for disposal.
    /// </summary>
    /// <param name="disposable">The object to be disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
    public void RegisterForDispose(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        Add(disposable);
    }

    /// <summary>
    ///  Registers an object for asynchronous disposal.
    /// </summary>
    /// <param name="disposable">The object to be disposed asynchronously.</param>
    /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
    public void RegisterForDisposeAsync(IAsyncDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        Add(disposable);
    }

    /// <summary>
    /// Executes all attached callbacks synchronously.
    /// </summary>
    public void Dispose()
    {
        var exceptions = new ExceptionAggregator();
        ExecuteCallbacks(callbacks.AsReadOnlySpan(), ref exceptions);

        if (rest is not null)
        {
            ExecuteCallbacks(CollectionsMarshal.AsSpan(rest), ref exceptions);
            rest.Clear();
        }

        this = default;
        exceptions.ThrowIfNeeded();

        static void ExecuteCallbacks(ReadOnlySpan<object?> callbacks, ref ExceptionAggregator aggregator)
        {
            Task t;

            foreach (var cb in callbacks)
            {
                try
                {
                    switch (cb)
                    {
                        case null:
                            return;
                        case Action callback:
                            callback();
                            break;
                        case Func<ValueTask> callback:
                            using (t = callback().AsTask())
                            {
                                t.Wait();
                            }

                            break;
                        case IDisposable disposable:
                            // IDisposable in synchronous implementation has higher priority than IAsyncDisposable
                            disposable.Dispose();
                            break;
                        case IAsyncDisposable disposable:
                            using (t = disposable.DisposeAsync().AsTask())
                            {
                                t.Wait();
                            }

                            break;
                    }
                }
                catch (Exception e)
                {
                    aggregator.Add(e);
                }
            }
        }
    }

    /// <summary>
    /// Executes all attached callbacks asynchronously.
    /// </summary>
    /// <returns>The task representing asynchronous execution.</returns>
    public readonly async ValueTask DisposeAsync()
    {
        var exceptions = BoxedValue<ExceptionAggregator>.Box(new());
        await ExecuteCallbacksAsync(callbacks, exceptions).ConfigureAwait(false);

        if (rest is not null)
        {
            await ExecuteCallbacksAsync(rest, exceptions).ConfigureAwait(false);
            rest.Clear();
        }

        exceptions.Value.ThrowIfNeeded();

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask ExecuteCallbacksAsync<T>(T callbacks, BoxedValue<ExceptionAggregator> exceptions)
            where T : ITuple
        {
            for (int i = 0, count = callbacks.Length; i < count; i++)
            {
                try
                {
                    switch (callbacks[i])
                    {
                        case null:
                            return;
                        case Action callback:
                            callback();
                            break;
                        case Func<ValueTask> callback:
                            await callback().ConfigureAwait(false);
                            break;
                        case IAsyncDisposable disposable:
                            // IAsyncDisposable in asynchronous implementation has higher priority than IDisposable
                            await disposable.DisposeAsync().ConfigureAwait(false);
                            break;
                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }
                catch (Exception e)
                {
                    exceptions.Value.Add(e);
                }
            }
        }
    }
}