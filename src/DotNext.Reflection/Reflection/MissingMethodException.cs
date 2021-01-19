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

        internal static MissingMethodException Create<T, TSignature>(string methodName)
            where TSignature : Delegate
        {
            var invokeMethod = DelegateType.GetInvokeMethod<TSignature>();
            return new MissingMethodException(typeof(T), methodName, invokeMethod.ReturnType, invokeMethod.GetParameterTypes());
        }

        internal static MissingMethodException Create<T, TArgs, TResult>(string methodName)
            where TArgs : struct
        {
            var type = typeof(TResult);
            if (type == typeof(Missing))
                type = typeof(void);
            return new MissingMethodException(typeof(T), methodName, type, Signature.Reflect(typeof(TArgs)).Parameters);
        }
    }
}