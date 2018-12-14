namespace MissingPieces
{
	/// <summary>
	/// Represents empty array singleton.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	public static class EmptyArray<T>
	{
		/// <summary>
		/// Empty array.
		/// </summary>
		public static readonly T[] Value = new T[0];
	}
}
