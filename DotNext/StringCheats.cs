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
    }
}