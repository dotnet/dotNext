using System.Reflection;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Basic interface for all reflected members.
	/// </summary>
	/// <typeparam name="M">Type of reflected member.</typeparam>
	public interface IMember<out M>: ICustomAttributeProvider
		where M : MemberInfo
	{
		/// <summary>
		/// Name of member.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Member metadata.
		/// </summary>
		M RuntimeMember { get; }
	}
}