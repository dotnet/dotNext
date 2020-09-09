using System;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    public static partial class Sequence
    {
        /// <summary>
        /// Gets enumerable collection created from generator method.
        /// </summary>
        /// <typeparam name="T">The type of the elements returned by generator method.</typeparam>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Generator<T>
        {
            /// <summary>
            /// The enumerator over elements returned by generator method.
            /// </summary>
            [StructLayout(LayoutKind.Auto)]
            public struct Enumerator
            {
                private readonly Func<Optional<T>>? generator;
                private Optional<T> current;

                internal Enumerator(Func<Optional<T>>? generator)
                {
                    this.generator = generator;
                    current = Optional<T>.None;
                }

                /// <summary>
                /// Gets the element in the collection at the current position of the enumerator.
                /// </summary>
                /// <exception cref="InvalidOperationException">The enumerator is empty.</exception>
                public readonly T Current
                    => current.TryGet(out var result, out var isNull) || isNull ? result : throw new InvalidOperationException();

                /// <summary>
                /// Advances the enumerator to the next element.
                /// </summary>
                /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if
                /// the enumerator has passed the end of the collection.</returns>
                public bool MoveNext()
                {
                    Optional<T> current;
                    if (generator is null)
                        current = default;
                    else
                        this.current = current = generator();

                    return !current.IsUndefined;
                }
            }

            private readonly Func<Optional<T>>? generator;

            internal Generator(Func<Optional<T>> generator)
                => this.generator = generator;

            /// <summary>
            /// Gets enumerator over elements to be returned by generator method.
            /// </summary>
            /// <returns>The enumerator over elements.</returns>
            public Enumerator GetEnumerator() => new Enumerator(generator);
        }

        /// <summary>
        /// Converts generator function to enumerable collection.
        /// </summary>
        /// <param name="generator">Stateful generator function.</param>
        /// <typeparam name="T">The type of the elements in the collection.</typeparam>
        /// <returns>The enumerable collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
        public static Generator<T> ToEnumerable<T>(this Func<Optional<T>> generator)
            => new Generator<T>(generator ?? throw new ArgumentNullException(nameof(generator)));
    }
}