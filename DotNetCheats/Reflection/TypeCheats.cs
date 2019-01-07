using System;
using System.Reflection;

namespace Cheats.Reflection
{
	/// <summary>
	/// Various extension methods for type reflection.
	/// </summary>
	public static class Types
	{
		private static readonly TypeSwitch<TypeCode> TypeCodes = TypeSwitch<TypeCode>.Define()
					.Add<DBNull>(TypeCode.DBNull)
					.Add<byte>(TypeCode.Byte)
					.Add<sbyte>(TypeCode.SByte)
					.Add<short>(TypeCode.Int16)
					.Add<ushort>(TypeCode.UInt16)
					.Add<int>(TypeCode.Int32)
					.Add<uint>(TypeCode.UInt32)
					.Add<long>(TypeCode.Int64)
					.Add<ulong>(TypeCode.UInt64)
					.Add<decimal>(TypeCode.Decimal)
					.Add<DateTime>(TypeCode.DateTime)
					.Add<float>(TypeCode.Single)
					.Add<double>(TypeCode.Double)
					.Add<string>(TypeCode.String);

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
			if(t is null)
				return TypeCode.Empty;
			else if(TypeCodes.Match(t).TryGet(out var code))
				return code;
			else
				return TypeCode.Object;
		}
	}
}