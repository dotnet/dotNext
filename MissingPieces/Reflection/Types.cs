using System;

namespace MissingPieces.Reflection
{
	public static class Types
	{
		public static bool IsGenericInstanceOf(this Type type, Type genericDefinition)
			=> type.IsGenericType &&
				!type.IsGenericTypeDefinition &&
				type.GetGenericTypeDefinition() == genericDefinition;
	}
}
