using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;

namespace DotNext.Reflection
{
	/// <summary>
	/// Various extension methods for type reflection.
	/// </summary>
	public static class Types
	{
		public static MethodInfo GetMethod<D>(this Type type, string name, BindingFlags flags)
			where D: MulticastDelegate
			=> type.GetMethod(name, flags, Type.DefaultBinder, typeof(D).GetInvokeMethod().GetParameterTypes(), Array.Empty<ParameterModifier>());

		private static Type FindGenericInstance(this Type type, Type genericDefinition)
		{
			bool IsGenericInstanceOf(Type candidate)
				=> candidate.IsGenericType && !candidate.IsGenericTypeDefinition && candidate.GetGenericTypeDefinition() == genericDefinition;

			if(genericDefinition.IsInterface)
			{
				foreach(var iface in type.GetInterfaces())
					if(IsGenericInstanceOf(iface))
						return iface;
			}
			else
				while(!(type is null))
					if(IsGenericInstanceOf(type))
						return type;
					else
						type = type.BaseType;
			return null;
		}

		public static bool IsGenericInstanceOf(this Type type, Type genericDefinition)
			=> !(FindGenericInstance(type, genericDefinition) is null);

		public static Type[] GetGenericArguments(this Type type, Type genericDefinition)
			=> FindGenericInstance(type, genericDefinition)?.GetGenericArguments() ?? Array.Empty<Type>();
				
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

		public static Type MakeTaskType(this Type returnType)
			=> returnType == typeof(void) ? typeof(Task) : typeof(Task<>).MakeGenericType(returnType);

		public static Type GetTaskType(this Type taskType)
		{
			var result = FindGenericInstance(taskType, typeof(Task<>));
			if(!(result is null))
				return result.GetGenericArguments()[0];
			else if(typeof(Task).IsAssignableFrom(taskType))
				return typeof(void);
			else
				return null;
		}

        public static Type GetCollectionElementType(this Type collectionType, out Type enumerableInterface)
        {
			enumerableInterface = FindGenericInstance(collectionType, typeof(IEnumerable<>));
            if(!(enumerableInterface is null))
				return enumerableInterface.GetGenericArguments()[0];
            else if(typeof(IEnumerable).IsAssignableFrom(collectionType))
            {
                enumerableInterface = typeof(IEnumerable);
                return typeof(object);
            }
            else
            {
                enumerableInterface = null;
                return null;
            }
		}

        public static Type GetCollectionElementType(this Type collectionType)
            => collectionType.GetCollectionElementType(out _);

        public static MethodInfo GetDisposeMethod(this Type type)
        {
            const string DisposeMethodName = nameof(IDisposable.Dispose);
            const BindingFlags PublicInstanceMethod = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
            return typeof(IDisposable).IsAssignableFrom(type) ?
                typeof(IDisposable).GetMethod(DisposeMethodName) :
                type.GetMethod(DisposeMethodName, PublicInstanceMethod, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
        }

        public static bool IsAssignableFromWithoutBoxing(this Type to, Type from)
            => to == from || !from.IsValueType && to.IsAssignableFrom(from);
    }
}