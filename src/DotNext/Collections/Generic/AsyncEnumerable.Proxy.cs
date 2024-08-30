namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    internal sealed class Proxy<T>(IEnumerable<T> enumerable) : IAsyncEnumerable<T>
    {
        internal sealed class Enumerator(IEnumerable<T> enumerable, CancellationToken token) : Disposable, IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> enumerator = enumerable.GetEnumerator();

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

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
            => new Enumerator(enumerable, token);
    }
}