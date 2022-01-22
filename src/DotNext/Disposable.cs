using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

using static Runtime.Intrinsics;

/// <summary>
/// Provides implementation of dispose pattern.
/// </summary>
/// <seealso cref="IDisposable"/>
/// <seealso cref="IAsyncDisposable"/>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose">Implementing Dispose method</seealso>
public abstract class Disposable : IDisposable
{
    private volatile bool disposed;

    /// <summary>
    /// Indicates that this object is disposed.
    /// </summary>
    protected bool IsDisposed => disposed;

    private string ObjectName => GetType().Name;

    /// <summary>
    /// Throws exception if this object is disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Object is disposed.</exception>
    protected void ThrowIfDisposed()
    {
        if (IsDisposed)
            Throw();

        [DoesNotReturn]
        [StackTraceHidden]
        void Throw() => throw new ObjectDisposedException(ObjectName);
    }

    /// <summary>
    /// Gets a task representing <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    protected Task DisposedTask => Task.FromException(new ObjectDisposedException(ObjectName));

    /// <summary>
    /// Returns a task representing <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    /// <typeparam name="T">The type of the task.</typeparam>
    /// <returns>The task representing <see cref="ObjectDisposedException"/> exception.</returns>
    protected Task<T> GetDisposedTask<T>()
        => Task.FromException<T>(new ObjectDisposedException(ObjectName));

    /// <summary>
    /// Attempts to complete the task with <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    /// <param name="source">The task completion source.</param>
    /// <typeparam name="T">The type of the task.</typeparam>
    /// <returns><see langword="true"/> if operation was successful; otherwise, <see langword="false"/>.</returns>
    protected bool TrySetDisposedException<T>(TaskCompletionSource<T> source)
        => source.TrySetException(new ObjectDisposedException(ObjectName));

    /// <summary>
    /// Attempts to complete the task with <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    /// <param name="source">The task completion source.</param>
    /// <returns><see langword="true"/> if operation was successful; otherwise, <see langword="false"/>.</returns>
    protected bool TrySetDisposedException(TaskCompletionSource source)
        => source.TrySetException(new ObjectDisposedException(ObjectName));

    /// <summary>
    /// Releases managed and unmanaged resources associated with this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Finalize()"/>.</param>
    protected virtual void Dispose(bool disposing) => disposed = true;

    /// <summary>
    /// Releases managed resources associated with this object asynchronously.
    /// </summary>
    /// <remarks>
    /// This method makes sense only if derived class implements <see cref="IAsyncDisposable"/> interface.
    /// </remarks>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    protected virtual ValueTask DisposeAsyncCore()
    {
        Dispose(true);
        return ValueTask.CompletedTask;
    }

    private async ValueTask DisposeAsyncImpl()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources associated with this object asynchronously.
    /// </summary>
    /// <remarks>
    /// If derived class implements <see cref="IAsyncDisposable"/> then <see cref="IAsyncDisposable.DisposeAsync"/>
    /// can be trivially implemented through delegation of the call to this method.
    /// </remarks>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    protected ValueTask DisposeAsync() => disposed ? ValueTask.CompletedTask : DisposeAsyncImpl();

    /// <summary>
    /// Releases all resources associated with this object.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes many objects.
    /// </summary>
    /// <param name="objects">An array of objects to dispose.</param>
    public static void Dispose(IEnumerable<IDisposable?> objects)
    {
        foreach (var obj in objects)
            obj?.Dispose();
    }

    /// <summary>
    /// Disposes many objects.
    /// </summary>
    /// <param name="objects">An array of objects to dispose.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public static async ValueTask DisposeAsync(IEnumerable<IAsyncDisposable?> objects)
    {
        foreach (var obj in objects)
        {
            if (obj is not null)
                await obj.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes many objects in safe manner.
    /// </summary>
    /// <param name="objects">An array of objects to dispose.</param>
    public static void Dispose(params IDisposable?[] objects)
    {
        for (nint i = 0; i < GetLength(objects); i++)
            objects[i]?.Dispose();
    }

    /// <summary>
    /// Disposes many objects in safe manner.
    /// </summary>
    /// <param name="objects">An array of objects to dispose.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public static ValueTask DisposeAsync(params IAsyncDisposable?[] objects)
        => DisposeAsync(objects.AsEnumerable());

    /// <summary>
    /// Finalizes this object.
    /// </summary>
    ~Disposable() => Dispose(false);
}