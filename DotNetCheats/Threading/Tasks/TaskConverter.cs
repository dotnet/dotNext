using System;
using System.Threading.Tasks;

namespace Cheats.Threading.Tasks
{
    public static class TaskConverter
    {
		public static async Task<O> Map<I, O>(this Task<I> task, Converter<I, O> converter)
			=> converter(await task);

		public static async Task<Optional<O>> Map<I, O>(this Task<Optional<I>> task, Converter<I, O> converter)
			=> (await task).Map(converter);

		public static async Task<O> FlatMap<I, O>(this Task<I> task, Converter<I, Task<O>> converter)
			=> await converter(await task);

		public static async Task<Optional<O>> FlatMap<I, O>(this Task<Optional<I>> task, Converter<I, Optional<O>> converter)
			=> (await task).FlatMap(converter);

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
	}
}