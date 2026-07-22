using System.Collections;

namespace DotNext.Collections.Generic;

public static partial class Collection
{
    /// <summary>
    /// Skip <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IEnumerable<T> SkipNulls<T>(this IEnumerable<T?> collection)
        where T : class
        => new NotNullEnumerable<T>(collection);
}

file sealed class NotNullEnumerable<T>(IEnumerable<T?> enumerable) : IEnumerable<T>
    where T : class
{
    private sealed class Enumerator : Disposable, IEnumerator<T>
    {
        private readonly IEnumerator<T?> enumerator;
        private T? current;

        internal Enumerator(IEnumerable<T?> enumerable)
            => enumerator = enumerable.GetEnumerator();

        public T Current => current ?? throw new InvalidOperationException();

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is { } currentValue)
                {
                    current = currentValue;
                    return true;
                }
            }

            return false;
        }

        public void Reset() => enumerator.Reset();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                current = null;
                enumerator.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    public IEnumerator<T> GetEnumerator() => new Enumerator(enumerable);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}