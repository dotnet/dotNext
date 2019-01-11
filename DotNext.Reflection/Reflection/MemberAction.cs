namespace Cheats.Reflection
{
	/// <summary>
	/// Represents action performed to property of field.
	/// </summary>
	public enum MemberAction: byte
	{
		/// <summary>
		/// Gets value of field or property.
		/// </summary>
		GetValue = 0,

		/// <summary>
		/// Sets value of field or property.
		/// </summary>
		SetValue
	}
}
