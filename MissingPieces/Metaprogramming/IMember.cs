using System.Reflection;

namespace MissingPieces.Metaprogramming
{
	internal interface IMember<out M>
		where M : MemberInfo
	{
		string Name { get; }
		M Member { get; }
		bool Exists { get; }
	}
}