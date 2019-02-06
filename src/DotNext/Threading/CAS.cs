namespace DotNext.Threading
{
	/// <summary>
	/// Represents generic Compare-And-Set (CAS) operation.
	/// </summary>
	/// <typeparam name="T">Type of operands of CAS operation.</typeparam>
	/// <param name="value">Reference to a value to be modified.</param>
	/// <param name="expected">The expected value.</param>
	/// <param name="update">The new value.</param>
	/// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
	internal delegate bool CAS<T>(ref T value, T expected, T update);
}
