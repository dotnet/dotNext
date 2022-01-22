using System.Diagnostics;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Specialized;

using static Runtime.Intrinsics;

public partial class TypeMap<TValue>
{
    /// <summary>
    /// Gets the enumerator over the values.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private readonly Entry[] entries;
        private nint index;

        internal Enumerator(Entry[] entries)
        {
            this.entries = entries;
            index = -1;
        }

        /// <summary>
        /// Gets the current element.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ref TValue Current => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Value!;

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
                    if (Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), nextIndex).HasValue)
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets enumerator over the values.
    /// </summary>
    /// <returns>The enumerator over the values.</returns>
    public Enumerator GetEnumerator() => new(entries);
}