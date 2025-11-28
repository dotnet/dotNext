using System.Collections;

namespace DotNext.Collections.Generic;

/// <summary>
/// Represents a collection that exposes the typed enumerator.
/// </summary>
/// <typeparam name="TEnumerator">The type of the enumerator.</typeparam>
/// <typeparam name="T"></typeparam>
public interface IEnumerable<out TEnumerator, out T> : IEnumerable<T>
    where TEnumerator : struct, IEnumerator<TEnumerator, T>
{
    /// <summary>
    /// Gets the typed enumerator.
    /// </summary>
    /// <returns>The typed enumerator.</returns>
    new TEnumerator GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => TEnumerator.ToEnumerator(GetEnumerator());

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => TEnumerator.ToEnumerator(GetEnumerator());

    /// <summary>
    /// Gets the async enumerator wrapper.
    /// </summary>
    /// <param name="enumerator">The synchronous enumerator</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>The asynchronous wrapper over <typeparamref name="TEnumerator"/>.</returns>
    protected static IAsyncEnumerator<T> GetAsyncEnumerator(TEnumerator enumerator, CancellationToken token)
        => TEnumerator.ToEnumerator(enumerator, token);
}