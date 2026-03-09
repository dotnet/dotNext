using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Generic;

/// <summary>
/// Represents ad-hoc enumerator implemented as value type to avoid enumerator allocation
/// and prevent the compiler to generate <see cref="IDisposable.Dispose()"/> call.
/// </summary>
/// <remarks>
/// The enumerator doesn't implement <see cref="IEnumerator{T}"/> interface implicitly
/// but can be converted to it by using regular type cast.
/// </remarks>
/// <typeparam name="TSelf">The value type that implements an enumerator.</typeparam>
/// <typeparam name="T">The type of objects to enumerate.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IEnumerator<TSelf, out T> : IResettable
    where TSelf : struct, IEnumerator<TSelf, T>
    where T : allows ref struct
{
    /// <inheritdoc cref="IEnumerator.MoveNext()"/>
    bool MoveNext();

    /// <inheritdoc cref="IEnumerator{T}.Current"/>
    T Current { get; }

    /// <inheritdoc cref="IEnumerator.Reset()"/>
    void IResettable.Reset() => throw new NotSupportedException();

    /// <summary>
    /// Converts ad-hoc enumerator to a generic enumerator.
    /// </summary>
    /// <param name="enumerator">Ad-hoc enumerator.</param>
    /// <returns>The enumerator over values of type <typeparamref name="T"/>.</returns>
    internal static virtual IEnumerator<T> ToEnumerator(in TSelf enumerator)
        => new BoxedEnumerator<TSelf, T>(enumerator);

    /// <summary>
    /// Converts ad-hoc enumerator to a generic enumerator.
    /// </summary>
    /// <param name="enumerator">Ad-hoc enumerator.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>The enumerator over values of type <typeparamref name="T"/>.</returns>
    internal static virtual IAsyncEnumerator<T> ToAsyncEnumerator(in TSelf enumerator, CancellationToken token)
        => new BoxedEnumerator<TSelf, T>(enumerator, token);
}

file sealed class BoxedEnumerator<TEnumerator, T>(in TEnumerator enumerator, CancellationToken token = default) : IEnumerator<T>, IAsyncEnumerator<T>
    where TEnumerator : struct, IEnumerator<TEnumerator, T>
    where T : allows ref struct
{
    private TEnumerator enumerator = enumerator;
    
    public T Current => enumerator.Current;

    object? IEnumerator.Current
    {
        get
        {
            var elementType = typeof(T);
            if (elementType.IsByRefLike)
                throw new NotSupportedException();

            var value = enumerator.Current;
            return elementType.IsValueType
                ? RuntimeHelpers.Box(ref Unsafe.As<T, byte>(ref value), typeof(T).TypeHandle)
                : Unsafe.As<T, object>(ref value);
        }
    }

    bool IEnumerator.MoveNext() => enumerator.MoveNext();

    ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
        => token.IsCancellationRequested ? ValueTask.FromCanceled<bool>(token) : ValueTask.FromResult(enumerator.MoveNext());

    void IEnumerator.Reset() => enumerator.Reset();

    void IDisposable.Dispose() => enumerator = default;

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        enumerator = default;
        return ValueTask.CompletedTask;
    }
}