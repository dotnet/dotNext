using System.Collections;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Specialized;

using Generic;

public partial class ConcurrentTypeMap<TValue>
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

        private bool TryGetValue(Entry entry)
            => UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).TryGetValue(out current)
                : Unsafe.As<GenericEntry>(entry).TryGetValue(out current);

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
                    if (nextIndex >= Array.GetLength(entries))
                        break;

                    index = nextIndex;
                    var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), nextIndex);
                    if (TryGetValue(entry))
                        return true;
                }
            }

            return false;
        }

        /// <inheritdoc cref="IResettable.Reset()"/>
        void IResettable.Reset() => index = nuint.MaxValue;
    }

    /// <summary>
    /// Gets enumerator over the values.
    /// </summary>
    /// <returns>The enumerator over the values.</returns>
    public Enumerator GetEnumerator() => new(Volatile.Read(ref entries));

    /// <inheritdoc/>
    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => IEnumerator<TValue>.Create(GetEnumerator());
    
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => IEnumerator<TValue>.Create(GetEnumerator());
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

        /// <inheritdoc cref="IEnumerator{T}.Current"/>
        public readonly object Current => current ?? throw new InvalidOperationException();
        
        /// <inheritdoc cref="IResettable.Reset()"/>
        void IResettable.Reset() => index = 0;
    }
    
    /// <summary>
    /// Gets an enumerator over values in this map.
    /// </summary>
    /// <returns>The enumerator over values in this map.</returns>
    public Enumerator GetEnumerator() => new(entries);
    
    /// <inheritdoc/>
    IEnumerator<object> IEnumerable<object>.GetEnumerator() => IEnumerator<object>.Create(GetEnumerator());

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => IEnumerator<object>.Create(GetEnumerator());
}