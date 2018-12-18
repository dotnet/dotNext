using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace MissingPieces
{
	public static class Delegates
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

		public static MethodInfo GetInvokeMethod<D>()
			where D: Delegate
			=> Reflection.Types.GetInvokeMethod(typeof(D));

		public static D ConvertDelegate<D>(this Delegate d)
			where D : Delegate
			=> d.Method.CreateDelegate<D>(d.Target);
	}
}
