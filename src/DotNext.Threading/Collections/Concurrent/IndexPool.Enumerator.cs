using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Generic;
using Threading;

partial struct IndexPool
{
    /// <summary>
    /// Gets an enumerator over available indices in the pool.
    /// </summary>
    /// <remarks>
    /// The returned enumerator represents a snapshot of the pool at the time of the method call.
    /// </remarks>
    /// <returns>The enumerator over available indices in this pool.</returns>
    public readonly Enumerator GetEnumerator() => new(Atomic.Read(in bitmask), maxValue);
    
    /// <inheritdoc />
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());
    
    /// <summary>
    /// Represents an enumerator over available indices in the pool.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Enumerator, int>
    {
        private readonly int maxValue;
        private ulong bitmask;

        internal Enumerator(ulong bitmask, int maxValue)
        {
            this.bitmask = bitmask;
            this.maxValue = maxValue;
        }

        /// <summary>
        /// Gets the remaining number of elements to be returned by this enumerator.
        /// </summary>
        public readonly int RemainingCount => Math.Min(GetCount(bitmask), maxValue + 1);

        /// <summary>
        /// Gets the current index.
        /// </summary>
        public int Current { get; private set; }

        /// <summary>
        /// Advances to the next available index.
        /// </summary>
        /// <returns><see langword="true"/> if enumerator advanced successfully; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext() => (Current = MoveNext(ref bitmask)) <= maxValue;

        private static int MoveNext(ref ulong bitmask)
        {
            var newValue = bitmask & (bitmask - 1UL); // BLSR
            var result = BitOperations.TrailingZeroCount(bitmask);
            bitmask = newValue;
            return result;
        }
    }
}