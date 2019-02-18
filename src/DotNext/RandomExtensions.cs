using System;
using System.Buffers;
using System.Security.Cryptography;

namespace DotNext
{
    using Buffers;

    /// <summary>
    /// Provides random data generation.
    /// </summary>
    public static class RandomExtensions
    {
        private static readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Create(16, 50);

        private static string NextString(Random random, ReadOnlySpan<char> allowedChars, int length)
		{
			var result = new char[length];
			foreach(ref char element in result.AsSpan())
				element = allowedChars[random.Next(0, allowedChars.Length)];
			return new string(result);
		}
	
 		private static string NextString(RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, int length)
		{
			var result = new char[length];
			using(var buffer = new ArrayRental<byte>(ByteArrayPool, sizeof(int), true))
                foreach(ref char element in result.AsSpan())
                {
                    random.GetBytes(buffer, 0, sizeof(int));
                    var randomNumber = BitConverter.ToInt32(buffer, 0) % allowedChars.Length;
                    element = allowedChars[randomNumber];
                }
			return new string(result);
        }

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The array of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
		public static string NextString(this Random random, char[] allowedChars, int length)
			=> NextString(random, new ReadOnlySpan<char>(allowedChars), length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The string of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        public static string NextString(this Random random, string allowedChars, int length)
			=> NextString(random, allowedChars.AsSpan(), length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The array of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
		public static string NextString(this RandomNumberGenerator random, char[] allowedChars, int length)
			=> NextString(random, new ReadOnlySpan<char>(allowedChars), length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The string of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        public static string NextString(this RandomNumberGenerator random, string allowedChars, int length)
			=> NextString(random, allowedChars.AsSpan(), length);

        /// <summary>
        /// Generates random boolean value.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="trueProbability">A probability of <see langword="true"/> result (should be between 0.0 and 1.0).</param>
        /// <returns>Randomly generated boolean value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="trueProbability"/> value is invalid.</exception>
        public static bool NextBoolean(this Random random, double trueProbability = 0.5D)
            => trueProbability.Between(0D, 1D, BoundType.Closed) ? 
                    random.NextDouble() >= (1.0D - trueProbability) :
                    throw new ArgumentOutOfRangeException(nameof(trueProbability));

        /// <summary>
        /// Generates random non-negative random integer.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <returns>A 32-bit signed integer that is in range [0, <see cref="int.MaxValue"/>].</returns>
        public static int Next(this RandomNumberGenerator random)
        {
            using(var buffer = new ArrayRental<byte>(ByteArrayPool, sizeof(int), true))
            {
                random.GetBytes(buffer, 0, sizeof(int));
                return Math.Abs(BitConverter.ToInt32(buffer, 0));
            }
        }

        /// <summary>
        /// Generates random boolean value.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="trueProbability">A probability of <see langword="true"/> result (should be between 0.0 and 1.0).</param>
        /// <returns>Randomly generated boolean value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="trueProbability"/> value is invalid.</exception>
        public static bool NextBoolean(this RandomNumberGenerator random, double trueProbability = 0.5D)
            => trueProbability.Between(0D, 1D, BoundType.Closed) ? 
                    random.NextDouble() >= (1.0D - trueProbability) :
                    throw new ArgumentOutOfRangeException(nameof(trueProbability));

        /// <summary>
        /// Returns a random floating-point number that is greater than
        /// in range [0, 1).
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <returns>Randomly generated floating-point number.</returns>
        public static double NextDouble(this RandomNumberGenerator random)
        {
            using(var buffer = new ArrayRental<byte>(ByteArrayPool, sizeof(double), true))
            {
                random.GetBytes(buffer);
                var result = Math.Abs(BitConverter.ToDouble(buffer, 0));
                //normalize to range [0, 1)
                return result / (result + 1D);
            }
        }
    }
}