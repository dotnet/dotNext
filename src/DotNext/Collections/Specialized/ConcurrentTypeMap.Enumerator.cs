using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Specialized;

using static Runtime.Intrinsics;

public partial class ConcurrentTypeMap<TValue>
{
    /// <summary>
    /// Represents an enumerator over the values in the map.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private readonly Entry[] entries;
        private nint index;
        private TValue? current;

        internal Enumerator(Entry[] entries)
        {
            this.entries = entries;
            index = -1;
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
                for (nint nextIndex; ;)
                {
                    nextIndex = index + 1;
                    if (nextIndex >= GetLength(entries))
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
    }

    /// <summary>
    /// Gets enumerator over the values.
    /// </summary>
    /// <returns>The enumerator over the values.</returns>
    public Enumerator GetEnumerator() => new(Volatile.Read(ref entries));
}