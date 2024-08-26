using System.Collections;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Specialized;

using Generic;
using static Runtime.Intrinsics;

public partial class ConcurrentTypeMap<TValue> : IEnumerable<TValue>
{
    /// <summary>
    /// Represents an enumerator over the values in the map.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Enumerator, TValue>
    {
        private readonly Entry[] entries;
        private nuint index;
        private TValue? current;

        internal Enumerator(Entry[] entries)
        {
            this.entries = entries;
            index = nuint.MaxValue;
            current = default;
        }

        /// <summary>
        /// Gets the current element.
        /// </summary>
        public readonly TValue Current => current!;

        /// <summary>
        /// Advances this enumerator to the next element.
        /// </summary>
        /// <returns><see langword="true"/> if the next element is available in the collection; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (entries is not null)
            {
                for (nuint nextIndex;;)
                {
                    nextIndex = index + 1U;
                    if (nextIndex >= entries.GetLength())
                        break;

                    index = nextIndex;
                    var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), nextIndex);
                    if (entry.TryAcquireLock(HasValueState))
                    {
                        current = entry.Value;
                        entry.ReleaseLock(HasValueState);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <inheritdoc cref="IEnumerator{TSelf, T}.Reset()"/>
        void IEnumerator<Enumerator, TValue>.Reset() => index = nuint.MaxValue;
    }

    /// <summary>
    /// Gets enumerator over the values.
    /// </summary>
    /// <returns>The enumerator over the values.</returns>
    public Enumerator GetEnumerator() => new(Volatile.Read(ref entries));

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator()"/>
    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
        => GetEnumerator().ToClassicEnumerator<Enumerator, TValue>();

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator().ToClassicEnumerator<Enumerator, TValue>();
}

public partial class ConcurrentTypeMap
{
    /// <summary>
    /// Represents an enumerator over values stored in the map.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Enumerator, object>
    {
        private readonly Entry[] entries;
        private int index;
        private object? current;

        internal Enumerator(Entry[] entries) => this.entries = entries;

        /// <inheritdoc cref="IEnumerator.MoveNext()"/>
        public bool MoveNext()
        {
            while (entries is not null && index < entries.Length)
            {
                current = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index++).Value;
                if (current is not null)
                    return true;
            }

            return false;
        }

        /// <inheritdoc cref="IEnumerator.Current"/>
        public object Current => current ?? throw new InvalidOperationException();
        
        /// <inheritdoc cref="IEnumerator{TSelf, T}.Reset()"/>
        void IEnumerator<Enumerator, object>.Reset() => index = 0;
    }
    
    /// <summary>
    /// Gets an enumerator over values in this map.
    /// </summary>
    /// <returns>The enumerator over values in this map.</returns>
    public Enumerator GetEnumerator() => new(entries);

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator()"/>
    IEnumerator<object> IEnumerable<object>.GetEnumerator()
        => GetEnumerator().ToClassicEnumerator<Enumerator, object>();

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator().ToClassicEnumerator<Enumerator, object>();
}