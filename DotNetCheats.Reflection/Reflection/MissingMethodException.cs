using System;
using System.Collections.ObjectModel;

namespace Cheats.Reflection
{
    /// <summary>
	/// Indicates that requested method doesn't exist.
	/// </summary>
    public sealed class MissingMethodException: ConstraintViolationException
    {
        public MissingMethodException(Type declaringType, string methodName, Type returnType, params Type[] parameters)
            : base(declaringType, $"Method {methodName} with parameters [{parameters.ToString(",")}] and return type {returnType} doesn't exist in type {declaringType}")
        {
            MethodName = methodName;
            ReturnType = returnType;
            Parameters = Array.AsReadOnly(parameters);
        }

        public string MethodName { get; }
        public Type ReturnType { get; }
        public ReadOnlyCollection<Type> Parameters { get; }

        internal static MissingMethodException Create<T, D>(string methodName)
            where D: Delegate
        {
            var (parameters, returnType) = Delegates.GetInvokeMethod<D>().Decompose(method => method.GetParameterTypes(), method => method.ReturnType);
            return new MissingMethodException(typeof(T), methodName, returnType, parameters);
        }

        internal static MissingMethodException Create<T, A, R>(string methodName)
			where A: struct
			=> new MissingMethodException(typeof(T), methodName, typeof(R), Signature.Reflect(typeof(A)).Parameters);
	}
}