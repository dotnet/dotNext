using System;
using System.Collections.Generic;

namespace DotNext.Reflection
{
    /// <summary>
    /// Indicates that requested constructor doesn't exist.
    /// </summary>
    public sealed class MissingConstructorException : ConstraintViolationException
    {
        /// <summary>
        /// Initializes a new exception indicating that requested constructor doesn't exist.
        /// </summary>
        /// <param name="target">The inspected type.</param>
        /// <param name="parameters">An array of types representing constructor parameters.</param>
        public MissingConstructorException(Type target, params Type[] parameters)
            : base(target, ExceptionMessages.MissingCtor(target, parameters))
        {
            Parameters = parameters;
        }

        /// <summary>
        /// An array of types representing constructor parameters.
        /// </summary>
        public IReadOnlyList<Type> Parameters { get; }

        internal static MissingConstructorException Create<TSignature>()
            where TSignature : Delegate
        {
            var invokeMethod = DelegateType.GetInvokeMethod<TSignature>();
            return new MissingConstructorException(invokeMethod.ReturnType, invokeMethod.GetParameterTypes());
        }

        internal static MissingConstructorException Create<T, TArgs>()
            where TArgs : struct
            => new(typeof(T), Signature.Reflect(typeof(TArgs)).Parameters);
    }
}
