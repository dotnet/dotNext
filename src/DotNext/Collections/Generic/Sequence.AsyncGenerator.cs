using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Collections.Generic
{
    public static partial class Sequence
    {
        /// <summary>
        /// Gets enumerable collection created from generator method.
        /// </summary>
        /// <typeparam name="T">The type of the elements returned by generator method.</typeparam>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct AsyncGenerator<T> : IAsyncEnumerable<T>
        {
            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private readonly Func<CancellationToken, ValueTask<Optional<T>>>? generator;
                private readonly CancellationToken token;
                private Optional<T> current;

                internal Enumerator(Func<CancellationToken, ValueTask<Optional<T>>>? generator, CancellationToken token)
                {
                    this.generator = generator;
                    this.token = token;
                }

                /// <summary>
                /// Gets the element in the collection at the current position of the enumerator.
                /// </summary>
                /// <exception cref="InvalidOperationException">The enumerator is empty.</exception>
                public T Current
                    => current.TryGet(out var result, out var isNull) || isNull ? result! : throw new InvalidOperationException();

                /// <summary>
                /// Advances the enumerator to the next element.
                /// </summary>
                /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if
                /// the enumerator has passed the end of the collection.</returns>
                public async ValueTask<bool> MoveNextAsync()
                {
                    Optional<T> current;
                    if (generator is null)
                        current = default;
                    else
                        this.current = current = await generator(token).ConfigureAwait(false);

                    return !current.IsUndefined;
                }

                ValueTask IAsyncDisposable.DisposeAsync()
                {
                    current = default;
                    return default;
                }
            }

            private readonly Func<CancellationToken, ValueTask<Optional<T>>>? generator;

            internal AsyncGenerator(Func<CancellationToken, ValueTask<Optional<T>>> generator)
                => this.generator = generator;

            /// <summary>
            /// Gets enumerator over elements to be returned by generator method.
            /// </summary>
            /// <param name="token">The token that can be used to cancel the enumeration.</param>
            /// <returns>The enumerator over elements.</returns>
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default) => new Enumerator(generator, token);
        }

        /// <summary>
        /// Converts generator function to enumerable collection.
        /// </summary>
        /// <param name="generator">Stateful generator function.</param>
        /// <typeparam name="T">The type of the elements in the collection.</typeparam>
        /// <returns>The enumerable collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
        public static AsyncGenerator<T> ToAsyncEnumerable<T>(this Func<CancellationToken, ValueTask<Optional<T>>> generator)
            => new(generator ?? throw new ArgumentNullException(nameof(generator)));
    }
}