using System;

namespace DotNext
{
	/// <summary>
	/// Represents various extension methods for type <see cref="string"/>.
	/// </summary>
	public static class StringExtensions
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
		/// <param name="alt">Alternative string to be returned if original string is <see langword="null"/> or empty.</param>
		/// <returns>Original or alternative string.</returns>
		public static string IfNullOrEmpty(this string str, string alt)
            => string.IsNullOrEmpty(str) ? alt : str;

		/// <summary>
		/// Reverse string characters.
		/// </summary>
		/// <param name="str">The string to reverse.</param>
		/// <returns>The string with inversed orded of characters.</returns>
		public static string Reverse(this string str)
		{
			if(str.Length == 0)
				return str;
			var chars = str.ToCharArray();
			Array.Reverse(chars);
			return new string(chars);
		}

        /// <summary>
        /// Compares two string using <see cref="StringComparison.OrdinalIgnoreCase" />.
        /// </summary>
        /// <param name="strA">String A. Can be <see langword="null"/>.</param>
        /// <param name="strB">String B. Can be <see langword="null"/>.</param>
        /// <returns><see langword="true"/>, if the first string is equal to the second string; otherwise, <see langword="false"/>.</returns>
        public static bool IsEqualIgnoreCase (this string strA, string strB)
			=> string.Compare (strA, strB, StringComparison.OrdinalIgnoreCase) == 0;

        /// <summary>
        /// Trims the source string to specified length if it exceeds it.
        /// If source string is less that <paramref name="maxLength" /> then the source string returned.
        /// </summary>
        /// <param name="str">Source string.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed string value.</returns>
        public static string TrimLength(this string str, int maxLength)
            => str is null || str.Length <= maxLength ? str : str.Substring(0, maxLength);
  	}
}