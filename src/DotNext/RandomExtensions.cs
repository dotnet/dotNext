using System;
using System.Security.Cryptography;

namespace DotNext
{
    using BytesRental = Buffers.ArrayRental<byte>;
    using CharBuffer = Buffers.MemoryRental<char>;

    /// <summary>
    /// Provides random data generation.
    /// </summary>
    public static class RandomExtensions
    {
        internal static readonly int BitwiseHashSalt = new Random().Next();

        private interface IRandomStringGenerator
        {
            void NextString(Span<char> buffer, ReadOnlySpan<char> allowedChars);
        }

        private readonly struct PseudoRandomStringGenerator : IRandomStringGenerator
        {
            private readonly Random rng;

            internal PseudoRandomStringGenerator(Random random) => rng = random;

            void IRandomStringGenerator.NextString(Span<char> buffer, ReadOnlySpan<char> allowedChars)
            {
                foreach (ref var element in buffer)
                    element = allowedChars[rng.Next(0, allowedChars.Length)];
            }
        }

        private readonly struct RandomStringGenerator : IRandomStringGenerator
        {
            private readonly RandomNumberGenerator rng;

            internal RandomStringGenerator(RandomNumberGenerator random) => rng = random;

            void IRandomStringGenerator.NextString(Span<char> buffer, ReadOnlySpan<char> allowedChars)
            {
                //TODO: byte array should be replaced with stack allocated Span in .NET Standard 2.1
                using (var bytes = new BytesRental(buffer.Length * sizeof(int)))
                {
                    rng.GetBytes((byte[])bytes, 0, bytes.Length);
                    var offset = 0;
                    foreach (ref var element in buffer)
                    {
                        var randomNumber = (BitConverter.ToInt32((byte[])bytes, offset) & int.MaxValue) % allowedChars.Length;
                        element = allowedChars[randomNumber];
                        offset += sizeof(int);
                    }
                }
            }
        }

        private static unsafe string NextString<TGenerator>(TGenerator generator, ReadOnlySpan<char> allowedChars, int length)
            where TGenerator : struct, IRandomStringGenerator
        {
            //TODO: should be reviewed for .NET Standard 2.1
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return string.Empty;
            const short smallStringLength = 1024;
            //use stack allocation for small strings, which is 99% of all use cases
            CharBuffer result = length <= smallStringLength ? stackalloc char[length] : new CharBuffer(length);
            try
            {
                generator.NextString(result.Span, allowedChars);
                fixed (char* ptr = result)
                    return new string(ptr, 0, length);
            }
            finally
            {
                result.Dispose();
            }
        }

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        public static string NextString(this Random random, ReadOnlySpan<char> allowedChars, int length)
            => NextString(new PseudoRandomStringGenerator(random), allowedChars, length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The array of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
		public static string NextString(this Random random, char[] allowedChars, int length)
            => NextString(random, new ReadOnlySpan<char>(allowedChars), length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The string of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        public static string NextString(this Random random, string allowedChars, int length)
            => NextString(random, allowedChars.AsSpan(), length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        public static string NextString(this RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, int length)
            => NextString(new RandomStringGenerator(random), allowedChars, length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The array of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
		public static string NextString(this RandomNumberGenerator random, char[] allowedChars, int length)
            => NextString(random, new ReadOnlySpan<char>(allowedChars), length);

        /// <summary>
        /// Generates random string of the given length.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <param name="allowedChars">The string of allowed characters for the random string.</param>
        /// <param name="length">The length of the random string.</param>
        /// <returns>Randomly generated string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
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
                    random.NextDouble() >= 1.0D - trueProbability :
                    throw new ArgumentOutOfRangeException(nameof(trueProbability));

        /// <summary>
        /// Generates random non-negative random integer.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <returns>A 32-bit signed integer that is in range [0, <see cref="int.MaxValue"/>].</returns>
        public static int Next(this RandomNumberGenerator random)
        {
            //TODO: GetBytes should work with ReadOnlySpan in .NET Standard 2.1
            var buffer = new byte[sizeof(int)];
            random.GetBytes(buffer, 0, buffer.Length);
            return BitConverter.ToInt32(buffer, 0) & int.MaxValue;  //remove sign bit. Abs function may cause OverflowException
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
        /// Returns a random floating-point number that is in range [0, 1).
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <returns>Randomly generated floating-point number.</returns>
        public static double NextDouble(this RandomNumberGenerator random)
        {
            double result = random.Next();
            //normalize to range [0, 1)
            return result / (result + 1D);
        }
    }
}