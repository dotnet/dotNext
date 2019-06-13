using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents election timeout.
    /// </summary>
    public struct ElectionTimeout
    {
        private readonly Random random;

        public ElectionTimeout(int lowerValue, int upperValue)
            : this(lowerValue, upperValue, new Random())
        {
            
        }

        private ElectionTimeout(int lowerValue, int upperValue, Random rng)
        {
            LowerValue = lowerValue;
            UpperValue = upperValue;
            random = rng;
        }

        /// <summary>
        /// Gets recommended election timeout.
        /// </summary>
        public static ElectionTimeout Recommended => new ElectionTimeout(150, 500);

        /// <summary>
        /// Generates random election timeout.
        /// </summary>
        /// <returns>The randomized election timeout.</returns>
        public int RandomTimeout()
            => random?.Next(LowerValue, UpperValue + 1) ?? 0;

        public int LowerValue { get; }

        public int UpperValue { get; }

        public ElectionTimeout ModifiedClone(int lowerValue, int upperValue)
            => new ElectionTimeout(lowerValue, upperValue, random);
    }
}
