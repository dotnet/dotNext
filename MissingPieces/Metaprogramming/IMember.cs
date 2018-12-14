using System.Reflection;

namespace MissingPieces.Metaprogramming
{
	internal interface IMember<out M>: IOptional
		where M : MemberInfo
	{
		string Name { get; }
		M Member { get; }
	}
}