using System;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents election timeout.
    /// </summary>
    public struct ElectionTimeout
    {
        private readonly Random random;

        public ElectionTimeout(int lowerValue, int upperValue)
        {
            LowerValue = lowerValue;
            UpperValue = upperValue;
            random = new Random();
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
    }
}
