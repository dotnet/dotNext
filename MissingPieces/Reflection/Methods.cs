using System;
using System.Reflection;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Various extension methods for method reflection.
	/// </summary>
	public static class Methods
	{
		public static Type[] GetParameterTypes(this MethodBase method)
            => method?.GetParameters().Map(p => p.ParameterType);
	}
}