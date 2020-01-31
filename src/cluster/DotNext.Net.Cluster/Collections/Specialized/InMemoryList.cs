using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct InMemoryList<T> : IReadOnlyList<T>
    {
        private readonly ReadOnlyMemory<T> memory;

        internal InMemoryList(ReadOnlyMemory<T> memory) => this.memory = memory;

        public T this[int index] => memory.Span[index];

        public int Count => memory.Length;

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < memory.Length; i++)
                yield return memory.Span[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static implicit operator InMemoryList<T>(Memory<T> memory) => new InMemoryList<T>(memory);
    }
}
