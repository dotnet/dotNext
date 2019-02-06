using System;

namespace DotNext
{
	/// <summary>
	/// Represents a static procedure with arbitrary number of arguments
	/// allocated on the stack.
	/// </summary>
	/// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
	/// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
	public delegate void Procedure<A>(in A arguments)
		where A : struct;

	/// <summary>
	/// Represents an instance procedure with arbitrary number of arguments
	/// allocated on the stack.
	/// </summary>
	/// <param name="this">Hidden <see langword="this"/> parameter.</param>
	/// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
	/// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
	/// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
	public delegate void Procedure<T, A>(in T @this, in A arguments);

	public static class Procedure
	{
		public static A ArgList<A>(this Procedure<A> procedure)
            where A: struct
            => new A();
        
        public static A ArgList<T, A>(this Procedure<T, A> procedure)
            where A: struct
            => new A();
		
		public static void Invoke<T>(this Procedure<T, ValueTuple> procedure, in T instance)
			=> procedure(in instance, in EmptyTuple.Value);
		
		public static void Invoke(this Procedure<ValueTuple> procedure)
			=> procedure(in EmptyTuple.Value);
		
		public static void Invoke<T, P>(this Procedure<T, ValueTuple<P>> procedure, in T instance, P arg)
			=> procedure(in instance, new ValueTuple<P>(arg));
		
		public static void Invoke<P>(this Procedure<ValueTuple<P>> procedure, P arg)
			=> procedure(new ValueTuple<P>(arg));
		
		public static void Invoke<T, P1, P2>(this Procedure<T, (P1, P2)> procedure, in T instance, P1 arg1, P2 arg2)
			=> procedure(in instance, (arg1, arg2));
		
		public static void Invoke<P1, P2>(this Procedure<(P1, P2)> procedure, P1 arg1, P2 arg2)
			=> procedure((arg1, arg2));
		
		public static void Invoke<T, P1, P2, P3>(this Procedure<T, (P1, P2, P3)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3)
			=> procedure(in instance, (arg1, arg2, arg3));
		
		public static void Invoke<P1, P2, P3>(this Procedure<(P1, P2, P3)> procedure, P1 arg1, P2 arg2, P3 arg3)
			=> procedure((arg1, arg2, arg3));
		
		public static void Invoke<T, P1, P2, P3, P4>(this Procedure<T, (P1, P2, P3, P4)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
			=> procedure(in instance, (arg1, arg2, arg3, arg4));
		
		public static void Invoke<P1, P2, P3, P4>(this Procedure<(P1, P2, P3, P4)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
			=> procedure((arg1, arg2, arg3, arg4));
		
		public static void Invoke<T, P1, P2, P3, P4, P5>(this Procedure<T, (P1, P2, P3, P4, P5)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
			=> procedure(in instance, (arg1, arg2, arg3, arg4, arg5));
		
		public static void Invoke<P1, P2, P3, P4, P5>(this Procedure<(P1, P2, P3, P4, P5)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
			=> procedure((arg1, arg2, arg3, arg4, arg5));
		
		public static void Invoke<T, P1, P2, P3, P4, P5, P6>(this Procedure<T, (P1, P2, P3, P4, P5, P6)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
			=> procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6));
		
		public static void Invoke<P1, P2, P3, P4, P5, P6>(this Procedure<(P1, P2, P3, P4, P5, P6)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
			=> procedure((arg1, arg2, arg3, arg4, arg5, arg6));
		
		public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
			=> procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7));
		
		public static void Invoke<P1, P2, P3, P4, P5, P6, P7>(this Procedure<(P1, P2, P3, P4, P5, P6, P7)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
			=> procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7));
		
		public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7, P8)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
			=> procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
		
		public static void Invoke<P1, P2, P3, P4, P5, P6, P7, P8>(this Procedure<(P1, P2, P3, P4, P5, P6, P7, P8)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
			=> procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
		
		public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7, P8, P9)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
			=> procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
		
		public static void Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9>(this Procedure<(P1, P2, P3, P4, P5, P6, P7, P8, P9)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
			=> procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
		
		public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7, P8, P9, P10)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
			=> procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
		
		public static void Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(this Procedure<(P1, P2, P3, P4, P5, P6, P7, P8, P9, P10)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
			=> procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
	}
}
