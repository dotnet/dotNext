using System;
using System.Collections.Generic;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
	/// Indicates that requested method doesn't exist.
	/// </summary>
    public sealed class MissingMethodException : ConstraintViolationException
    {
        /// <summary>
        /// Initializes a new exception indicating that requested method doesn't exist.
        /// </summary>
        /// <param name="declaringType">The inspected type.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="returnType">The return type of the missing method.</param>
        /// <param name="parameters">The parameters of the missing method.</param>
        public MissingMethodException(Type declaringType, string methodName, Type returnType, params Type[] parameters)
            : base(declaringType, ExceptionMessages.MissingMethod(methodName, parameters, returnType, declaringType))
        {
            MethodName = methodName;
            ReturnType = returnType;
            Parameters = parameters;
        }

        /// <summary>
        /// Gets name of missing method.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets return type of missing method.
        /// </summary>
        public Type ReturnType { get; }

        /// <summary>
        /// Gets parameters of missing method.
        /// </summary>
        public IReadOnlyList<Type> Parameters { get; }

        internal static MissingMethodException Create<T, D>(string methodName)
            where D : Delegate
        {
            var (parameters, returnType) = DelegateType.GetInvokeMethod<D>().Decompose(method => method.GetParameterTypes(), method => method.ReturnType);
            return new MissingMethodException(typeof(T), methodName, returnType, parameters);
        }

        internal static MissingMethodException Create<T, A, R>(string methodName)
            where A : struct
        {
            var type = typeof(R);
            if (type == typeof(Missing))
                type = typeof(void);
            return new MissingMethodException(typeof(T), methodName, type, Signature.Reflect(typeof(A)).Parameters);
        }
    }
}