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
        /// <summary>
        /// Initializes a new leader election timeout.
        /// </summary>
        /// <param name="lowerValue">The lower possible value of leader election timeout, in milliseconds.</param>
        /// <param name="upperValue">The upper possible value of leader election timeout, in milliseconds.</param>
        public ElectionTimeout(int lowerValue, int upperValue)
        {
            LowerValue = lowerValue > 0 ? lowerValue : throw new ArgumentOutOfRangeException(nameof(lowerValue));
            UpperValue = upperValue > 0 && upperValue < int.MaxValue ? upperValue : throw new ArgumentOutOfRangeException(nameof(upperValue));
        }

        /// <summary>
        /// Gets recommended election timeout.
        /// </summary>
        public static ElectionTimeout Recommended => new(150, 300);

        /// <summary>
        /// Generates random election timeout.
        /// </summary>
        /// <param name="random">The source of random values.</param>
        /// <returns>The randomized election timeout.</returns>
        public int RandomTimeout(Random random) => random.Next(LowerValue, UpperValue + 1);

        /// <summary>
        /// Gets lower possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int LowerValue { get; }

        /// <summary>
        /// Gets upper possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int UpperValue { get; }
    }

    /// <summary>
    /// Represents extension methods for <see cref="ElectionTimeout"/> value type.
    /// </summary>
    public static class ElectionTimeoutExtensions
    {
        /// <summary>
        /// Updates boundaries of the timeout.
        /// </summary>
        /// <param name="timeout">The election timeout to update.</param>
        /// <param name="lowerValue">The lower possible value of leader election timeout, in milliseconds.</param>
        /// <param name="upperValue">The upper possible value of leader election timeout, in milliseconds.</param>
        public static void Update(this ref ElectionTimeout timeout, int? lowerValue, int? upperValue)
            => timeout = new(lowerValue ?? timeout.LowerValue, upperValue ?? timeout.UpperValue);
    }
}
