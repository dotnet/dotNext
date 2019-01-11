using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cheats.Threading.Tasks
{
	public static class TaskCheats
	{
		public static async Task<O> Convert<I, O>(this Task<I> task, Converter<I, O> converter)
			=> converter(await task);

		public static async Task<Optional<O>> Convert<I, O>(this Task<Optional<I>> task, Converter<I, O> converter)
			=> (await task).Convert(converter);

		public static async Task<O> Convert<I, O>(this Task<I> task, Converter<I, Task<O>> converter)
			=> await converter(await task);

		public static async Task<T> Or<T>(this Task<Optional<T>> task, T defaultValue)
			=> (await task).Or(defaultValue);

		public static async Task<T> OrThrow<T, E>(this Task<Optional<T>> task)
			where E : Exception, new()
			=> (await task).OrThrow<E>();

		public static async Task<T> OrThrow<T, E>(this Task<Optional<T>> task, Func<E> exceptionFactory)
			where E : Exception
			=> (await task).OrThrow(exceptionFactory);

		public static async Task<T> OrInvoke<T>(this Task<Optional<T>> task, Func<T> defaultFunc)
			=> (await task).OrInvoke(defaultFunc);

		public static async Task<T> OrDefault<T>(this Task<Optional<T>> task)
			=> (await task).OrDefault();

		public static async Task<Optional<T>> If<T>(this Task<Optional<T>> task, Predicate<T> condition)
			=> (await task).If(condition);

		public static R GetResult<R>(this Task<R> task, TimeSpan timeout)
		{
			if (task.Wait(timeout))
				return task.Result;
			else
				throw new TimeoutException();
		}

		public static R GetResult<R>(this Task<R> task, CancellationToken token)
		{
			task.Wait(token);
			return task.Result;
		}
	}
}
