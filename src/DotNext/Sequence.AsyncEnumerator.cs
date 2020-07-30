using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    public static partial class Sequence
    {
        private sealed class AsyncEnumerable<T> : IAsyncEnumerable<T>
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
                {
                    if (token.IsCancellationRequested)
                        return new ValueTask<bool>(Task.FromCanceled<bool>(token));

                    return new ValueTask<bool>(enumerator.MoveNext());
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        enumerator.Dispose();
                    }

                    base.Dispose(disposing);
                }

                public ValueTask DisposeAsync()
                {
                    Dispose();
                    return new ValueTask();
                }
            }

            private readonly IEnumerable<T> enumerable;

            internal AsyncEnumerable(IEnumerable<T> enumerable)
                => this.enumerable = enumerable;

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
                => new Enumerator(enumerable, token);
        }
    }
}