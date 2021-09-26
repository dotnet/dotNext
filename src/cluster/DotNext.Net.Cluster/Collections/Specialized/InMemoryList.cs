using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

using Seq = Collections.Generic.Sequence;

[StructLayout(LayoutKind.Auto)]
internal readonly struct InMemoryList<T> : IReadOnlyList<T>
{
    private readonly ReadOnlyMemory<T> memory;

    internal InMemoryList(ReadOnlyMemory<T> memory) => this.memory = memory;

    public T this[int index] => memory.Span[index];

    public int Count => memory.Length;

    public IEnumerator<T> GetEnumerator() => Seq.ToEnumerator(memory);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static implicit operator InMemoryList<T>(Memory<T> memory) => new(memory);
}