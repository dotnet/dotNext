using System;
using System.Reflection;

namespace DotNext.Reflection
{
	/// <summary>
	/// Various extension methods for type reflection.
	/// </summary>
	public static class TypeCheats
	{
		public static bool IsGenericInstanceOf(this Type type, Type genericDefinition)
			=> type.IsGenericType &&
				!type.IsGenericTypeDefinition &&
				type.GetGenericTypeDefinition() == genericDefinition;
				
		internal static MethodInfo GetInvokeMethod(this Type delegateType)
			=> !(delegateType is null) && typeof(Delegate).IsAssignableFrom(delegateType) ?
			 delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly):
			 null;

		public static TypeCode GetTypeCode(this Type t)
		{
			if (t is null)
				return TypeCode.Empty;
			else if (t == typeof(bool))
				return TypeCode.Boolean;
			else if (t == typeof(byte))
				return TypeCode.Byte;
			else if (t == typeof(sbyte))
				return TypeCode.SByte;
			else if (t == typeof(short))
				return TypeCode.Int16;
			else if (t == typeof(ushort))
				return TypeCode.UInt16;
			else if (t == typeof(int))
				return TypeCode.Int32;
			else if (t == typeof(uint))
				return TypeCode.UInt32;
			else if (t == typeof(long))
				return TypeCode.Int64;
			else if (t == typeof(ulong))
				return TypeCode.UInt64;
			else if (t == typeof(float))
				return TypeCode.Single;
			else if (t == typeof(double))
				return TypeCode.Double;
			else if (t == typeof(string))
				return TypeCode.String;
			else if (t == typeof(DateTime))
				return TypeCode.DateTime;
			else if (t == typeof(decimal))
				return TypeCode.Decimal;
			else if (t == typeof(char))
				return TypeCode.Char;
			else if (t == typeof(DBNull))
				return TypeCode.DBNull;
			else
				return TypeCode.Object;
		}
	}
}