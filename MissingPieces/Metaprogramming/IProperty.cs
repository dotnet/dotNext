using System.Reflection;

namespace MissingPieces.Metaprogramming
{
	internal interface IProperty: IMember<PropertyInfo>
	{
		bool CanRead { get; }
		bool CanWrite { get; }
	}
}
