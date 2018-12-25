namespace MissingPieces.Reflection
{
	/// <summary>
	/// Represents action performed to event.
	/// </summary>
	public enum EventAction: byte
	{
		/// <summary>
		/// Attach event handler to event.
		/// </summary>
		AddHandler = 0,

		/// <summary>
		/// Detach event handler from event.
		/// </summary>
		RemoveHandler = 1
	}
}
