using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Generic;

partial struct PartitionedIndexPool
{
    /// <summary>
    /// Gets the enumerator over indices in this pool.
    /// </summary>
    /// <returns></returns>
    public Enumerator GetEnumerator() => new(partitions);

    /// <inheritdoc/>
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());
    
    /// <summary>
    /// Represents the enumerator over the pool.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Enumerator, int>
    {
        private readonly uint[] partitions;
        private uint snapshot;
        private int currentPartition;

        internal Enumerator(uint[] partitions)
        {
            this.partitions = partitions;
            snapshot = Volatile.Read(in partitions[0]);
        }
        
        /// <summary>
        /// Gets the current value.
        /// </summary>
        public int Current { get; private set; }

        private bool MoveToNextPartition()
        {
            var nextPartition = currentPartition + 1;
            if ((uint)nextPartition < (uint)partitions.Length)
            {
                snapshot = Volatile.Read(in partitions[currentPartition = nextPartition]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Advances to the next available index.
        /// </summary>
        /// <returns><see langword="true"/> if enumerator advanced successfully; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            do
            {
                var subIndex = MoveNext(ref snapshot);
                if (subIndex > MaxSubIndex)
                    continue;

                Current = (currentPartition << Shift) + subIndex;
                return true;
            } while (MoveToNextPartition());

            return false;
        }

        private static int MoveNext(ref uint bitmask)
        {
            var newValue = bitmask & (bitmask - 1U); // BLSR
            var result = BitOperations.TrailingZeroCount(bitmask);
            bitmask = newValue;
            return result;
        }
    }
}