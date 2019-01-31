using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
	public static class Tasks
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

        public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2) => (await task1, await task2);

        public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3) => (await task1, await task2, await task3);

        public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4) => (await task1, await task2, await task3, await task4);

        public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5) => (await task1, await task2, await task3, await task4, await task5);
    }
}
