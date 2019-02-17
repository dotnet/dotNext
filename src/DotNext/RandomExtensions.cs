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

		public static string NextString(this Random random, char[] allowedChars, int length)
			=> NextString(random, new ReadOnlySpan<char>(allowedChars), length);

		public static string NextString(this Random random, string allowedChars, int length)
			=> NextString(random, allowedChars.AsSpan(), length);
		
		public static string NextString(this RandomNumberGenerator random, char[] allowedChars, int length)
			=> NextString(random, new ReadOnlySpan<char>(allowedChars), length);
		
		public static string NextString(this RandomNumberGenerator random, string allowedChars, int length)
			=> NextString(random, allowedChars.AsSpan(), length);

        public static bool NextBoolean(this Random random) => Convert.ToBoolean(random.Next());

        public static int Next(this RandomNumberGenerator random)
        {
            using(var buffer = new ArrayRental<byte>(ByteArrayPool, sizeof(int), true))
            {
                random.GetBytes(buffer, 0, sizeof(int));
                return BitConverter.ToInt32(buffer, 0);
            }
        }

        public static bool NextBoolean(this RandomNumberGenerator random) => Convert.ToBoolean(random.Next());
        
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

        public static long NextInt64(this Random random) => random.NextDouble().BitCast<double, long>();

        public static long NextInt64(this RandomNumberGenerator random) => random.NextDouble().BitCast<double, long>();
    }
}