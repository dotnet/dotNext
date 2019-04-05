using System;
using System.Collections;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    internal readonly struct Enumerator<T, I, O>: IEnumerator<O>
        where T : struct, IEnumerator<I>
    {
        private readonly Converter<I, O> converter;
        private readonly T enumerator;

        internal Enumerator(Converter<I, O> converter, T enumerator)
        {
            this.converter = converter;
            this.enumerator = enumerator;
        }

        O IEnumerator<I>.Current => converter(enumerator.Current);

        bool IEnumerator.MoveNext() => enumerator.MoveNext();

        object IEnumerator.Current => converter(enumerator.Current);

        void IEnumerator.Reset() => enumerator.Reset;

        void IDisposable.Dispose()
        {
            enumerator.Dispose();
            this = default;
        }
    }
}