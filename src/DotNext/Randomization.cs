using System;
using System.Security.Cryptography;

namespace DotNext
{
    /// <summary>
    /// Provides random data generation.
    /// </summary>
    public static class Randomization
    {
        private static string NextString(Random random, ReadOnlySpan<char> allowedChars, int length)
		{
			var result = new char[length];
			foreach(ref char element in result.AsSpan())
				element = allowedChars[random.Next(0, allowedChars.Length)];
			return new string(result);
		}

        private static int Next(this RandomNumberGenerator random, byte[] buffer)
        {
            random.GetBytes(buffer);
            return BitConverter.ToInt32(buffer, 0);
        }
	
 		private static string NextString(RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, int length)
		{
			var result = new char[length];
			var buffer = new byte[sizeof(int)];
			foreach(ref char element in result.AsSpan())
			{
				random.GetBytes(buffer);
				var randomNumber = Math.Abs(random.Next(buffer)) % allowedChars.Length;
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

        public static int Next(this RandomNumberGenerator random) => random.Next(new byte[sizeof(int)]);

        public static bool NextBoolean(this RandomNumberGenerator random) => Convert.ToBoolean(random.Next());
        

        public static double NextDouble(this RandomNumberGenerator random)
        {
            var buffer = new byte[sizeof(double)];
            random.GetBytes(buffer);
            var result = Math.Abs(BitConverter.ToDouble(buffer, 0));
            //normalize to range [0, 1)
            return result / (result + 1D);
        }

        private static long ToInt64(int hi, int low) => ((long)hi << 32) | ((long)low & 0xFFFFFFFL);

        public static long NextInt64(this Random random) => ToInt64(random.Next(), random.Next());

        public static long NextInt64(this RandomNumberGenerator random)
        {
            var buffer = new byte[sizeof(int)];
            return ToInt64(random.Next(buffer), random.Next(buffer));
        }
    }
}