using System;

namespace DotNext.Net.ConsistentHash
{
    /// <summary>
    /// Represents JUMP consistent hash.
    /// </summary>
    /// <seealso href="https://arxiv.org/pdf/1406.2294.pdf">JUMP hash.</seealso>
    public static class JumpHash
    {
        /// <summary>
        /// Computes the bucket number.
        /// </summary>
        /// <remarks>
        /// The validation of <paramref name="buckets"/> parameter is the responsibility
        /// of the caller.
        /// </remarks>
        /// <param name="key">The key or key hash.</param>
        /// <param name="buckets">The maximum number of buckets.</param>
        /// <returns>The value in range [0..buckets).</returns>
        [CLSCompliant(false)]
        public static int GetBucket(ulong key, int buckets)
        {
            const double bitMask = (double)(1L << 31);
            var result = -1L;

            for (var j = 0L; j < buckets; )
            {
                result = j;
                key = unchecked((key * 2862933555777941757UL) + 1U);
                j = (long)((result + 1) * (bitMask / (double)((key >> 33) + 1U)));
            }

            return (int)result;
        }

        /// <summary>
        /// Computes the bucket number.
        /// </summary>
        /// <remarks>
        /// The validation of <paramref name="buckets"/> parameter is the responsibility
        /// of the caller.
        /// </remarks>
        /// <param name="key">The key or key hash.</param>
        /// <param name="buckets">The maximum number of buckets.</param>
        /// <returns>The value in range [0..buckets).</returns>
        public static int GetBucket(long key, int buckets)
            => GetBucket(unchecked((ulong)key), buckets);
    }
}