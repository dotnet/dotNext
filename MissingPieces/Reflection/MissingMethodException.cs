using System;
using System.Collections.ObjectModel;

namespace MissingPieces.Reflection
{
    /// <summary>
	/// Indicates that requested method doesn't exist.
	/// </summary>
    public sealed class MissingMethodException: ConstraintViolationException
    {
        private MissingMethodException(Type declaringType, string methodName, Type returnType, params Type[] parameters)
            : base($"Method {methodName} with parameters [{parameters.ToString(",")}] and return type {returnType} doesn't exist in type {declaringType}", declaringType)
        {
            MethodName = methodName;
            ReturnType = returnType;
            Parameters = Array.AsReadOnly(parameters);
        }

        public string MethodName { get; }
        public Type ReturnType { get; }
        public ReadOnlyCollection<Type> Parameters { get; }

        internal static MissingMethodException CreateFunc<T, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R));
        internal static MissingMethodException CreateFunc<T, P, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P));
        
        internal static MissingMethodException CreateFunc<T, P1, P2, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2));

        internal static MissingMethodException CreateFunc<T, P1, P2, P3, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3));
        
        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4));
        
        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, P5, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5));

        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, P5, P6, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6));
        
        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7));

        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8));
    
        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8), typeof(P9));
    
        internal static MissingMethodException CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>(string methodName) 
            => new MissingMethodException(typeof(T), methodName, typeof(R), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8), typeof(P9), typeof(P10));

		//

		internal static MissingMethodException CreateAction<T>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void));

		internal static MissingMethodException CreateAction<T, P>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P));

		internal static MissingMethodException CreateAction<T, P1, P2>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2));

		internal static MissingMethodException CreateAction<T, P1, P2, P3>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4, P5>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4, P5, P6>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4, P5, P6, P7>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8), typeof(P9));

		internal static MissingMethodException CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(string methodName)
			=> new MissingMethodException(typeof(T), methodName, typeof(void), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8), typeof(P9), typeof(P10));
	}
}