using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Cheats
{
	public static class DelegateCheats
	{
		public static Task InvokeAsync<E>(this EventHandler<E> handler, object sender, E args, bool parallel = true)
		{
			if (handler == null)
				return Task.CompletedTask;
			else if (parallel)
			{
				var handlers = handler?.GetInvocationList() ?? Array.Empty<EventHandler<E>>();
				switch (handlers.LongLength)
				{
					case 0:
						return Task.CompletedTask;
					case 1:
						return handler.InvokeAsync(sender, args, false);
					default:
						var tasks = new LinkedList<Task>();
						foreach (EventHandler<E> h in handlers)
							tasks.AddLast(h.InvokeAsync(sender, args, false));
						return Task.WhenAll(tasks);
				}
			}
			else
				return Task.Factory.StartNew(() => handler(sender, args));
		}

		public static Task InvokeAsync(this EventHandler handler, object sender, EventArgs args, bool parallel = true)
		{
			if (handler == null)
				return Task.CompletedTask;
			else if (parallel)
			{
				var handlers = handler?.GetInvocationList() ?? Array.Empty<EventHandler>();
				switch (handlers.LongLength)
				{
					case 0:
						return Task.CompletedTask;
					case 1:
						return handler.InvokeAsync(sender, args, false);
					default:
						var tasks = new LinkedList<Task>();
						foreach (EventHandler h in handlers)
							tasks.AddLast(h.InvokeAsync(sender, args, false));
						return Task.WhenAll(tasks);
				}
			}
			else
				return Task.Factory.StartNew(() => handler(sender, args));
		}

		public static EventHandler<O> Contravariant<I, O>(this EventHandler<I> handler)
			where I : class
			where O : class, I
			=> handler.ConvertDelegate<EventHandler<O>>();

		public static D CreateDelegate<D>(this MethodInfo method, object target)
			where D : Delegate
			=> (D)method.CreateDelegate(typeof(D), target);

		public static D CreateDelegate<D>(this MethodInfo method)
			where D : Delegate
			=> method.CreateDelegate<D>(null);

		/// <summary>
		/// Returns special Invoke method generate for each delegate type.
		/// </summary>
		/// <typeparam name="D">Type of delegate.</typeparam>
		/// <returns>An object representing reflected method Invoke.</returns>
		public static MethodInfo GetInvokeMethod<D>()
			where D: Delegate
			=> Reflection.TypeCheats.GetInvokeMethod(typeof(D));

		/// <summary>
		/// Returns a new delegate of different type which
		/// points to the same method as original delegate.
		/// </summary>
		/// <param name="d">Delegate to convert.</param>
		/// <typeparam name="D">A new delegate type.</typeparam>
		/// <returns>A method wrapped into new delegate type.</returns>
		/// <exception cref="ArgumentException">Cannot convert delegate type.</exception>
		public static D ConvertDelegate<D>(this Delegate d)
			where D : Delegate
			=> d.Method.CreateDelegate<D>(d.Target);

		private static D CreateDelegateFromGenericMethod<D>(MethodInfo method, object target)
			where D: Delegate
		{
			var targetGenericParams = typeof(D).GetGenericArguments();
			return targetGenericParams.IsNullOrEmpty() ?
				throw new ArgumentException($"Type {typeof(D)} is not a generic delegate") :
				method.MakeGenericMethod(targetGenericParams).CreateDelegate<D>(target);
		}

		/// <summary>
		/// Reinterprets generic method represented
		/// by specified input delegate instance into
		/// new generic delegate type.
		/// </summary>
		/// <remarks>
		/// This method obtains generic definition of the method
		/// represented by input delegate. Then it obtains generic arguments
		/// of target delegate type, makes new generic method using these arguments
		/// and create a new delegate based on this method.
		/// </remarks>
		/// <param name="input"></param>
		/// <typeparam name="D">Generic delegate instance.</typeparam>
		/// <returns>Reinterpreted delegate; or null, if method is not generic method or delegate type <typeparamref name="D"/> is not generic type.</returns>
		public static D Reinterpret<D>(this Delegate input)
			where D: Delegate
			=> input.Method.IsGenericMethod ?
				CreateDelegateFromGenericMethod<D>(input.Method.GetGenericMethodDefinition(), input.Target) :
				input.ConvertDelegate<D>();
	}
}
