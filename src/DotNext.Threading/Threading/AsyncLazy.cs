using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using static Tasks.Synchronization;

/// <summary>
/// Provides support for asynchronous lazy initialization.
/// </summary>
/// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
public class AsyncLazy<T> : ISupplier<CancellationToken, Task<T>>
{
    private const string NotAvailable = "<NotAvailable>";
    private readonly bool resettable;
    private volatile Task<T>? task;

    // null or Func<Task<T>> or Func<CancellationToken, Task<T>>
    private MulticastDelegate? factory;

    /// <summary>
    /// Initializes a new instance of lazy value which is already computed.
    /// </summary>
    /// <param name="value">Already computed value.</param>
    public AsyncLazy(T value)
    {
        resettable = false;
        task = System.Threading.Tasks.Task.FromResult(value);
    }

    /// <summary>
    /// Initializes a new instance of lazy value.
    /// </summary>
    /// <param name="valueFactory">The function used to compute actual value.</param>
    /// <param name="resettable"><see langword="true"/> if previously computed value can be removed and computation executed again when it will be requested; <see langword="false"/> if value can be computed exactly once.</param>
    /// <exception cref="ArgumentException"><paramref name="valueFactory"/> is <see langword="null"/>.</exception>
    public AsyncLazy(Func<Task<T>> valueFactory, bool resettable = false)
    {
        factory = valueFactory ?? throw new ArgumentNullException(nameof(factory));
        this.resettable = resettable;
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
        this.resettable = resettable;
    }

    /// <summary>
    /// Gets a value that indicates whether a value has been computed.
    /// </summary>
    public bool IsValueCreated => task?.IsCompleted ?? false;

    /// <summary>
    /// Gets value if it is already computed.
    /// </summary>
    public Result<T>? Value => task.TryGetResult();

    /// <inheritdoc />
    Task<T> ISupplier<CancellationToken, Task<T>>.Invoke(CancellationToken token)
        => WithCancellation(token);

    [MethodImpl(MethodImplOptions.Synchronized)]
    private Task<T> GetAsync(CancellationToken token)
    {
        var t = task;
        return t is null or { IsCanceled: true } ? task = InvokeFactory(token) : t;
    }

    /// <summary>
    /// Gets already completed task or invokes the factory.
    /// </summary>
    /// <remark>
    /// The canceled task will be restarted automatically even if the lazy container is not resettable.
    /// </remark>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Lazy representation of the value.</returns>
    public Task<T> WithCancellation(CancellationToken token = default)
    {
        var t = task;
        return t is null or { IsCanceled: true } ? GetAsync(token) : t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<T> InvokeFactory(CancellationToken token)
    {
        Debug.Assert(factory is Func<Task<T>> or Func<CancellationToken, Task<T>>);

        var result = factory is Func<Task<T>> ? Unsafe.As<Func<Task<T>>>(factory).Invoke() : Unsafe.As<Func<CancellationToken, Task<T>>>(factory).Invoke(token);
        return resettable ? result : GetResultAndRemoveFactoryAsync(result);
    }

    private async Task<T> GetResultAndRemoveFactoryAsync(Task<T> task)
    {
        Debug.Assert(factory is not null);

        var canceled = false;
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            canceled = e is OperationCanceledException;
            throw;
        }
        finally
        {
            if (!canceled)
                factory = null;
        }
    }

    /// <summary>
    /// Gets task representing asynchronous computation of lazy value.
    /// </summary>
    /// <seealso cref="WithCancellation"/>
    public Task<T> Task => WithCancellation(CancellationToken.None);

    /// <summary>
    /// Removes already computed value from the current object.
    /// </summary>
    /// <returns><see langword="true"/> if previous value is removed successfully; <see langword="false"/> if value is still computing or this instance is not resettable.</returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool Reset()
    {
        if (resettable && (task is null or { IsCompleted: true }))
        {
            task = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets awaiter for the asynchronous operation responsible for computing value.
    /// </summary>
    /// <returns>The task awaiter.</returns>
    public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();

    /// <summary>
    /// Configures an awaiter used to await asynchronous lazy initialization.
    /// </summary>
    /// <param name="continueOnCapturedContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
    /// <returns>An object used to await asynchronous lazy initialization.</returns>
    public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        => Task.ConfigureAwait(continueOnCapturedContext);

    /// <summary>
    /// Returns textual representation of this object.
    /// </summary>
    /// <returns>The string representing this object.</returns>
    public override string? ToString()
    {
        var task = this.task;
        return task?.Status switch
        {
            null => NotAvailable,
            TaskStatus.RanToCompletion => task.Result?.ToString(),
            TaskStatus status => $"<{status}>",
        };
    }
}