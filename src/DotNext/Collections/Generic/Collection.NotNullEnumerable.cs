using System.Collections;

namespace DotNext.Collections.Generic;

public static partial class Collection
{
    private sealed class NotNullEnumerable<T> : IEnumerable<T>
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
                for (T? current; enumerator.MoveNext();)
                {
                    current = enumerator.Current;
                    if (current is not null)
                    {
                        this.current = current;
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

        private readonly IEnumerable<T?> enumerable;

        internal NotNullEnumerable(IEnumerable<T?> enumerable)
            => this.enumerable = enumerable;

        public IEnumerator<T> GetEnumerator() => new Enumerator(enumerable);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}