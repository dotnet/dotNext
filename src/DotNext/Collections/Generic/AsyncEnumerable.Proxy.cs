namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    internal sealed class Proxy<T> : IAsyncEnumerable<T>
    {
        internal sealed class Enumerator : Disposable, IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> enumerator;
            private readonly CancellationToken token;

            internal Enumerator(IEnumerable<T> enumerable, CancellationToken token)
            {
                enumerator = enumerable.GetEnumerator();
                this.token = token;
            }

            public T Current => enumerator.Current;

            public ValueTask<bool> MoveNextAsync()
                => token.IsCancellationRequested ? ValueTask.FromCanceled<bool>(token) : new(enumerator.MoveNext());

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    enumerator.Dispose();
                }

                base.Dispose(disposing);
            }

            public new ValueTask DisposeAsync() => base.DisposeAsync();
        }

        private readonly IEnumerable<T> enumerable;

        internal Proxy(IEnumerable<T> enumerable)
            => this.enumerable = enumerable;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
            => new Enumerator(enumerable, token);
    }
}