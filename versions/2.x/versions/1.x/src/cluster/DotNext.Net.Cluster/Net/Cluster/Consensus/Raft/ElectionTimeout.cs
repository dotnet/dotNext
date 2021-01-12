using System;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents leader election timeout.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ElectionTimeout
    {
        private readonly Random random;

        /// <summary>
        /// Initializes a new leader election timeout.
        /// </summary>
        /// <param name="lowerValue">The lower possible value of leader election timeout, in milliseconds.</param>
        /// <param name="upperValue">The upper possible value of leader election timeout, in milliseconds.</param>
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
        public static ElectionTimeout Recommended => new ElectionTimeout(150, 300);

        /// <summary>
        /// Generates random election timeout.
        /// </summary>
        /// <returns>The randomized election timeout.</returns>
        public int RandomTimeout() => random?.Next(LowerValue, UpperValue + 1) ?? 0;

        /// <summary>
        /// Gets lower possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int LowerValue { get; }

        /// <summary>
        /// Gets upper possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int UpperValue { get; }

        /// <summary>
        /// Modifies the boundary of the timeout.
        /// </summary>
        /// <param name="lowerValue">The lower possible value of leader election timeout, in milliseconds.</param>
        /// <param name="upperValue">The upper possible value of leader election timeout, in milliseconds.</param>
        /// <returns>The modified leader election timeout.</returns>
        public ElectionTimeout Modify(int lowerValue, int upperValue)
            => new ElectionTimeout(lowerValue, upperValue, random);
    }
}
