using System;
using System.Reflection;

namespace MissingPieces.Metaprogramming
{
	public interface IMethod<out M, out D>: IMember<M>
		where M: MethodBase
		where D: Delegate
	{
		/// <summary>
		/// Gets delegate which can be used to invoke method.
		/// </summary>
		D Invoker { get; }
	}

	/// <summary>
	/// Represents regular method.
	/// </summary>
	/// <typeparam name="D">Type of delegate describing method signature.</typeparam>
	public interface IMethod<out D>: IMethod<MethodInfo, D>
		where D: Delegate
	{

	}
}
