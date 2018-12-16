using System;
using System.Reflection;

namespace MissingPieces
{
	public static class Delegates
	{
		public static EventHandler<O> Contravariant<I, O>(this EventHandler<I> handler)
			where I : class
			where O : class, I
			=> handler.ConvertDelegate<EventHandler<O>>();

		public static D CreateDelegate<D>(this MethodInfo method, object target)
			where D : Delegate
			=> (D)method.CreateDelegate(typeof(D), target);

		public static MethodInfo GetInvokeMethod<D>()
			where D: Delegate
			=> Reflection.Types.GetInvokeMethod(typeof(D));

		public static D ConvertDelegate<D>(this Delegate d)
			where D : Delegate
			=> d.Method.CreateDelegate<D>(d.Target);
	}
}
