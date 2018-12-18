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
}
