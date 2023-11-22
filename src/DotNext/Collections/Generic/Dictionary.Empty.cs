using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Generic;

public static partial class Dictionary
{
    [DebuggerDisplay("Count = 0")]
    private sealed class EmptyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        internal static readonly EmptyDictionary<TKey, TValue> Instance = new();

        private EmptyDictionary()
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int IReadOnlyCollection<KeyValuePair<TKey, TValue>>.Count => 0;

        TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] => throw new KeyNotFoundException();

        bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => false;

        bool IReadOnlyDictionary<TKey, TValue>.TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            value = default;
            return false;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Array.Empty<TKey>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Array.Empty<TValue>();

        private static IEnumerator<KeyValuePair<TKey, TValue>> EmptyEnumerator
            => Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            => EmptyEnumerator;

        IEnumerator IEnumerable.GetEnumerator() => EmptyEnumerator;
    }
}