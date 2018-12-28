using System;
using System.Reflection;

namespace DotNetCheats.Reflection
{
	/// <summary>
	/// Various extension methods for type reflection.
	/// </summary>
	public static class Types
	{
		public static bool IsGenericInstanceOf(this Type type, Type genericDefinition)
			=> type.IsGenericType &&
				!type.IsGenericTypeDefinition &&
				type.GetGenericTypeDefinition() == genericDefinition;
				
		internal static MethodInfo GetInvokeMethod(this Type delegateType)
			=> !(delegateType is null) && typeof(Delegate).IsAssignableFrom(delegateType) ?
			 delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly):
			 null;
	}
}