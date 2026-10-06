using System.Collections;

namespace DotNext.Collections.Generic;

public static partial class Collection
{
    /// <summary>
    /// Skips <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IEnumerable<T> SkipNulls<T>(this IEnumerable<T?> collection)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        
        return new NotNullEnumerable<T?, T, NotNullFilter<T>>(collection);
    }

    /// <summary>
    /// Skips <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IEnumerable<T> SkipNulls<T>(this IEnumerable<T?> collection)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(collection);

        return new NotNullEnumerable<T?, T, HasValueFilter<T>>(collection);
    }

    /// <summary>
    /// Returns only elements which has values.
    /// </summary>
    /// <typeparam name="T">Type of the monad.</typeparam>
    /// <typeparam name="TMonad">Type of the elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection of values.</returns>
    public static IEnumerable<T> Flatten<T, TMonad>(this IEnumerable<TMonad> collection)
        where T : notnull
        where TMonad : struct, IOptionMonad<T>
    {
        ArgumentNullException.ThrowIfNull(collection);

        return new NotNullEnumerable<TMonad, T, NotEmptyFilter<TMonad, T>>(collection);
    }
}

file sealed class NotNullEnumerable<TInput, TOutput, TFilter>(IEnumerable<TInput?> enumerable) : IEnumerable<TOutput>
    where TOutput : notnull
    where TFilter : INullabilityFilter<TInput, TOutput>, allows ref struct
{
    private sealed class Enumerator : Disposable, IEnumerator<TOutput>
    {
        private readonly IEnumerator<TInput?> enumerator;
        private TOutput? current;

        internal Enumerator(IEnumerable<TInput?> enumerable)
            => enumerator = enumerable.GetEnumerator();

        public TOutput Current => current!;

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            while (enumerator.MoveNext())
            {
                if (TFilter.TryGet(enumerator.Current, out current))
                    return true;
            }

            return false;
        }

        public void Reset() => enumerator.Reset();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                current = default;
                enumerator.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    public IEnumerator<TOutput> GetEnumerator() => new Enumerator(enumerable);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}