using Array = System.Array;

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
		/// <param name="alt">Alternative </param>
		/// <returns>Original or alternative </returns>
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
  	}
}