using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

/// <summary>
/// Provides implementation of dispose pattern.
/// </summary>
/// <seealso cref="IDisposable"/>
/// <seealso cref="IAsyncDisposable"/>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose">Implementing Dispose method</seealso>
public abstract class Disposable : IDisposable
{
    private volatile ObjectState state;

    /// <summary>
    /// Indicates that this object is disposed.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    protected bool IsDisposed => state is ObjectState.Disposed;

    /// <summary>
    /// Indicates that <see cref="DisposeAsync()"/> is called but not yet completed.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    protected bool IsDisposing => state is ObjectState.Disposing;

    /// <summary>
    /// Indicates that <see cref="DisposeAsync()"/> is called.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    protected bool IsDisposingOrDisposed => state is not ObjectState.NotDisposed;

    private string ObjectName => GetType().Name;

    /// <summary>
    /// Creates a new instance of <see cref="ObjectDisposedException"/> class.
    /// </summary>
    /// <returns>A new instance of the exception that can be thrown by the caller.</returns>
    protected ObjectDisposedException CreateException() => new(ObjectName);

    /// <summary>
    /// Gets a task representing <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    protected Task DisposedTask => Task.FromException(CreateException());

    /// <summary>
    /// Returns a task representing <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    /// <typeparam name="T">The type of the task.</typeparam>
    /// <returns>The task representing <see cref="ObjectDisposedException"/> exception.</returns>
    protected Task<T> GetDisposedTask<T>()
        => Task.FromException<T>(CreateException());

    /// <summary>
    /// Attempts to complete the task with <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    /// <param name="source">The task completion source.</param>
    /// <typeparam name="T">The type of the task.</typeparam>
    /// <returns><see langword="true"/> if operation was successful; otherwise, <see langword="false"/>.</returns>
    protected bool TrySetDisposedException<T>(TaskCompletionSource<T> source)
        => source.TrySetException(CreateException());

    /// <summary>
    /// Attempts to complete the task with <see cref="ObjectDisposedException"/> exception.
    /// </summary>
    /// <param name="source">The task completion source.</param>
    /// <returns><see langword="true"/> if operation was successful; otherwise, <see langword="false"/>.</returns>
    protected bool TrySetDisposedException(TaskCompletionSource source)
        => source.TrySetException(CreateException());

    /// <summary>
    /// Releases managed and unmanaged resources associated with this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Finalize()"/>.</param>
    protected virtual void Dispose(bool disposing)
        => state = ObjectState.Disposed;

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
    protected ValueTask DisposeAsync() => Interlocked.CompareExchange(ref state, ObjectState.Disposing, ObjectState.NotDisposed) switch
    {
        ObjectState.NotDisposed => DisposeAsyncImpl(),
        ObjectState.Disposing => DisposeAsyncCore(),
        _ => ValueTask.CompletedTask,
    };

    /// <summary>
    /// Starts disposing this object.
    /// </summary>
    /// <returns><see langword="true"/> if cleanup operations can be performed; <see langword="false"/> if the object is already disposing.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected bool TryBeginDispose()
        => Interlocked.CompareExchange(ref state, ObjectState.Disposing, ObjectState.NotDisposed) is ObjectState.NotDisposed;

    /// <summary>
    /// Releases all resources associated with this object.
    /// </summary>
    [SuppressMessage("Design", "CA1063", Justification = "No need to call Dispose(true) multiple times")]
    public void Dispose()
    {
        Dispose(TryBeginDispose());
        GC.SuppressFinalize(this);
    }

    private static void Dispose<TDisposable, TEnumerator>(TEnumerator enumerator)
        where TDisposable : IDisposable?
        where TEnumerator : IEnumerator<TDisposable>, allows ref struct
    {
        while (enumerator.MoveNext())
        {
            enumerator.Current?.Dispose();
        }
    }

    /// <summary>
    /// Disposes many objects.
    /// </summary>
    /// <param name="objects">An array of objects to dispose.</param>
    public static void Dispose(IEnumerable<IDisposable?> objects)
    {
        using var enumerator = objects.GetEnumerator();
        Dispose(enumerator);
    }

    /// <summary>
    /// Disposes many objects.
    /// </summary>
    /// <param name="objects">An array of objects to dispose.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public static async ValueTask DisposeAsync(params IEnumerable<IAsyncDisposable?> objects)
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
    public static void Dispose<TDisposable>(params ReadOnlySpan<TDisposable> objects)
        where TDisposable : IDisposable?
        => Dispose<TDisposable, ReadOnlySpan<TDisposable>.Enumerator>(objects.GetEnumerator());

    /// <summary>
    /// Finalizes this object.
    /// </summary>
    ~Disposable() => Dispose(false);
    
    private enum ObjectState
    {
        NotDisposed = 0,
        Disposing,
        Disposed,
    }
}