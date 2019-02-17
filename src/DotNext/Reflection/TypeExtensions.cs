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
	public static class TypeExtensions
    {
        private static bool IsGenericParameter(Type type)
        {
            if (type.IsByRef)
                type = type.GetElementType();
            return type.IsGenericParameter;
        }

        /// <summary>
        /// Searches for the generic method in the specified type.
        /// </summary>
        /// <param name="type">The type in which search should be performed.</param>
        /// <param name="methodName">The name of the method to get.</param>
        /// <param name="flags">A bitmask that specify how the search is conducted.</param>
        /// <param name="genericParamCount">Number of generic parameters in the method signature.</param>
        /// <param name="parameters">An array representing the number, order, and type of the parameters for the method to get.</param>
        /// <returns>Search result; or <see langword="null"/> if search criteria is invalid or method doesn't exist.</returns>
        /// <remarks>
        /// Element of the array <paramref name="parameters"/> should be <see langword="null"/> if this parameter of generic type.
        /// </remarks>
        public static MethodInfo GetMethod(this Type type, string methodName, BindingFlags flags, long genericParamCount, params Type[] parameters)
        {
            foreach(var method in type.GetMethods(flags))
                if(method.Name == methodName && method.GetGenericArguments().LongLength == genericParamCount)
                {
                    var success = false;
                    //check signature
                    var actualParams = method.GetParameterTypes();
                    if (success = (actualParams.LongLength == parameters.LongLength))
                        for (var i = 0L; i < actualParams.LongLength; i++)
                        {
                            var actual = actualParams[i];
                            var expected = parameters[i];
                            if (success = ((IsGenericParameter(actual) && expected is null) || actual == expected))
                                continue;
                            else
                                break;
                        }
                    if (success)
                        return method;
                }
            return null;
        }

        /// <summary>
        /// Searches for the specified method whose parameters match the specified argument types, using the specified binding constraints.
        /// </summary>
        /// <param name="type">The type in which search should be performed.</param>
        /// <param name="name">The name of the method to get.</param>
        /// <param name="flags">A bitmask that specify how the search is conducted.</param>
        /// <param name="parameters">An array representing the number, order, and type of the parameters for the method to get.</param>
        /// <returns>Search result; or <see langword="null"/> if search criteria is invalid or method doesn't exist.</returns>
		public static MethodInfo GetMethod(this Type type, string name, BindingFlags flags, params Type[] parameters)
			=> type.GetMethod(name, flags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());

		internal static Type FindGenericInstance(this Type type, Type genericDefinition)
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

        /// <summary>
        /// Determines whether the type is an instance of the specified generic type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="genericDefinition">Generic type definition.</param>
        /// <returns><see langword="true"/>, if the type is an instance of the specified generic type; otherwise, <see langword="false"/>.</returns>
        /// <example>
        /// <code>
        /// typeof(byte[]).IsGenericInstanceOf(typeof(IEnumerable&lt;&gt;));    //returns true
        /// typeof(List&lt;int&gt;).IsGenericInstanceOf(typeof(List&lt;int&gt;));   //returns true
        /// </code>
        /// </example>
		public static bool IsGenericInstanceOf(this Type type, Type genericDefinition)
			=> !(FindGenericInstance(type, genericDefinition) is null);

        /// <summary>
        /// Returns actual generic arguments passed into generic type definition implemented by the input type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericDefinition"></param>
        /// <returns></returns>
        /// <example>
        /// <code>
        /// var elementTypes = typeof(byte[]).IsGenericInstanceOf(typeof(IEnumerable&lt;&gt;));
        /// elementTypes[0] == typeof(byte); //true
        /// </code>
        /// </example>
		public static Type[] GetGenericArguments(this Type type, Type genericDefinition)
			=> FindGenericInstance(type, genericDefinition)?.GetGenericArguments() ?? Array.Empty<Type>();

        /// <summary>
        /// Gets type code for the specified type.
        /// </summary>
        /// <param name="t">The type to convert into type code.</param>
        /// <returns>Type code.</returns>
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

        /// <summary>
        /// Indicates that object of one type can be implicitly converted into another whithout boxing.
        /// </summary>
        /// <param name="to">Type of conversion result.</param>
        /// <param name="from">The type check.</param>
        /// <returns><see langword="true"/> if <paramref name="from"/> is implicitly convertible into <paramref name="to"/> without boxing.</returns>
        /// <seealso cref="Type.IsAssignableFrom(Type)"/>
        /// <example>
        /// <code>
        /// typeof(object).IsAssignableFrom(typeof(int)); //true
        /// typeof(object).IsAssignableFromWithoutBoxing(typeof(int)); //false
        /// typeof(object).IsAssignableFrom(typeof(string));    //true
        /// typeof(object).IsAssignableFromWithoutBoxing(typeof(string));//true
        /// </code>
        /// </example>
        public static bool IsAssignableFromWithoutBoxing(this Type to, Type from)
            => to == from || !from.IsValueType && to.IsAssignableFrom(from);
        
        /// <summary>
        /// Casts an object to the class, value type or interface.
        /// </summary>
        /// <param name="type">The type t</param>
        /// <param name="obj">The object to be cast.</param>
        /// <returns>The object after casting, or <see langword="null"/> if <paramref name="obj"/> is <see langword="null"/>.</returns>
        /// <exception cref="InvalidCastException">
        /// If the object is not <see langword="null"/> and is not assignable to the <paramref name="type"/>; 
        /// or if object is <see langword="null"/> and <paramref name="type"/> is value type.
        /// </exception>
        public static object Cast(this Type type, object obj)
        {
            if(obj is null)
                return type.IsValueType ? new InvalidCastException(ExceptionMessages.CastNullToValueType) : null;
            else if(type.IsAssignableFrom(obj.GetType()))
                return obj;
            else
                throw new InvalidCastException();
        }
    }
}