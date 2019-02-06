using System;
using System.Security.Cryptography;

namespace DotNext
{
	/// <summary>
	/// Represents various extension methods for type <see cref="string"/>.
	/// </summary>
	public static class Strings
    {
		/// <summary>
		/// Returns alternative string if first string argument 
		/// is <see langword="null"/> or empty.
		/// </summary>
		/// <example>
		/// This method is equivalent to
		/// <code>
		/// var result = string.IsNullOrEmpty(str) ? alt : str;
		/// </code>
		/// </example>
		/// <param name="str">A string to check.</param>
		/// <param name="alt">Alternative </param>
		/// <returns>Original or alternative </returns>
		public static string IfNullOrEmpty(this string str, string alt)
            => string.IsNullOrEmpty(str) ? alt : str;

		private static string RandomString(Random random, ReadOnlySpan<char> allowedChars, int length)
		{
			var result = new char[length];
			foreach(ref char element in result.AsSpan())
				element = allowedChars[random.Next(0, allowedChars.Length)];
			return new string(result);
		}

		private static string RandomString(RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, int length)
		{
			var result = new char[length];
			var buffer = new byte[sizeof(int)];
			foreach(ref char element in result.AsSpan())
			{
				random.GetBytes(buffer);
				var randomNumber = Math.Abs(BitConverter.ToInt32(buffer, 0)) % allowedChars.Length;
				element = allowedChars[randomNumber];
			}
			return new string(result);
		}

		public static string RandomString(this Random random, char[] allowedChars, int length)
			=> RandomString(random, new ReadOnlySpan<char>(allowedChars), length);

		public static string RandomString(this Random random, string allowedChars, int length)
			=> RandomString(random, allowedChars.AsSpan(), length);
		
		public static string RandomString(this RandomNumberGenerator random, char[] allowedChars, int length)
			=> RandomString(random, new ReadOnlySpan<char>(allowedChars), length);
		
		public static string RandomString(this RandomNumberGenerator random, string allowedChars, int length)
			=> RandomString(random, allowedChars.AsSpan(), length);
    }
}