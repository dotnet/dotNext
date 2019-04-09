using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace DotNext
{
    using Buffers;

    /// <summary>
    /// Provides random data generation.
    /// </summary>
    public static class RandomExtensions
    {
        internal static readonly int BitwiseHashSalt = new Random().Next();

        private interface IRandomCharacterGenerator
        {
            char NextChar(ReadOnlySpan<char> allowedChars);
        }

        private readonly struct RandomCharacterGenerator: IRandomCharacterGenerator
        {
            private readonly Random random;

            internal RandomCharacterGenerator(Random random) => this.random = random;

            char IRandomCharacterGenerator.NextChar(ReadOnlySpan<char> allowedChars) => allowedChars[random.Next(0, allowedChars.Length)];
        }

        private readonly struct RNGCharacterGenerator: IRandomCharacterGenerator
        {
            private readonly byte[] buffer;
            private readonly RandomNumberGenerator random;

            internal RNGCharacterGenerator(RandomNumberGenerator random)
            {
                this.random = random;
                buffer = new byte[sizeof(int)];
            }


            char IRandomCharacterGenerator.NextChar(ReadOnlySpan<char> allowedChars)
            {
                random.GetBytes(buffer, 0, sizeof(int));
                var randomNumber = Math.Abs(BitConverter.ToInt32(buffer, 0) % allowedChars.Length);   
                return allowedChars[randomNumber];
            }
        }

        private static unsafe string NextString<R>(ref R generator, ReadOnlySpan<char> allowedChars, int length)
            where R: struct, IRandomCharacterGenerator
        {
            if(length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            else if(length == 0)
                return "";
            //use stack allocation for small strings
            var result = length < 1024 ? stackalloc char[length] : new Span<char>(new char[length]);
            foreach(ref char element in result)
                element = generator.NextChar(allowedChars);
            fixed(char* ptr = result)
                return new string(ptr, 0, length);
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
		{
            var generator = new RandomCharacterGenerator(random);
            return NextString(ref generator, allowedChars, length);
        }

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
        {
            var generator = new RNGCharacterGenerator(random);
            return NextString(ref generator, allowedChars, length);
        }

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
                    random.NextDouble() >= (1.0D - trueProbability) :
                    throw new ArgumentOutOfRangeException(nameof(trueProbability));

        /// <summary>
        /// Generates random non-negative random integer.
        /// </summary>
        /// <param name="random">The source of random numbers.</param>
        /// <returns>A 32-bit signed integer that is in range [0, <see cref="int.MaxValue"/>].</returns>
        public static int Next(this RandomNumberGenerator random)
        {
            var buffer = new byte[sizeof(int)];
            random.GetBytes(buffer, 0, sizeof(int));
            return Math.Abs(BitConverter.ToInt32(buffer, 0));
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
            var buffer = new byte[sizeof(double)];
            random.GetBytes(buffer);
            var result = Math.Abs(BitConverter.ToDouble(buffer, 0));
            //normalize to range [0, 1)
            return result / (result + 1D);
        }
    }
}