namespace Cheats
{
	/// <summary>
	/// Indicates that content of the implementing class or struct 
	/// doesn't have meaningful payload.
	/// </summary>
	/// <remarks>It is recommended to implement this interface explicitly.</remarks>
	public interface IOptional
	{
		/// <summary>
		/// Indicates that this object has meaningful payload.
		/// </summary>
		bool IsPresent { get; }
	}
}
