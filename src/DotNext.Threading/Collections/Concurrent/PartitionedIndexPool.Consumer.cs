using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Generic;

partial struct PartitionedIndexPool
{
    /// <summary>
    /// Takes all the values in the pool.
    /// </summary>
    /// <returns>The consumer that removes the values from the pool.</returns>
    public Consumer TakeAll() => new(partitions);
    
    /// <summary>
    /// Represents removing consumer.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Consumer : IEnumerable<int>
    {
        private readonly uint[] partitions;

        internal Consumer(uint[] partitions) => this.partitions = partitions;

        /// <summary>
        /// Gets removing enumerator.
        /// </summary>
        /// <returns>The removing enumerator.</returns>
        public Enumerator GetEnumerator() => new(partitions);

        /// <inheritdoc/>
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());

        /// <summary>
        /// The removing enumerator.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<Enumerator, int>
        {
            private readonly uint[] partitions;
            private int currentPartition;

            internal Enumerator(uint[] partitions)
                => this.partitions = partitions;

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public int Current { get; private set; }

            /// <summary>
            /// Advances and removes the next value from the pool.
            /// </summary>
            /// <returns><see langword="true"/> if the next value is successfully taken; otherwise, <see langword="false"/>.</returns>
            public bool MoveNext()
            {
                for (int subIndex; (uint)currentPartition < (uint)partitions.Length; currentPartition++)
                {
                    subIndex = Take(ref partitions[currentPartition]);
                    if (subIndex <= MaxSubIndex)
                    {
                        Current = (currentPartition << Shift) + subIndex;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}