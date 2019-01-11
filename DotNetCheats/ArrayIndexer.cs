namespace Cheats
{
	/// <summary>
	/// Represents array indexer delegate which can be used
	/// to read and modify array element during iteration.
	/// </summary>
	/// <typeparam name="T">Type of array element.</typeparam>
	/// <param name="index">Element index.</param>
	/// <param name="element">Mutable managed pointer to array element.</param>
	public delegate void ArrayIndexer<T>(long index, ref T element);
}
