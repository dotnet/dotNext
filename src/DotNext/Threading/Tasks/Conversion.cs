using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
	/// <summary>
	/// Provides task result conversion methods.
	/// </summary>
	public static class Conversion
	{
		public static async Task<O> Convert<I, O>(this Task<I> task, Converter<I, O> converter)
			=> converter(await task);

		public static async Task<Optional<O>> Convert<I, O>(this Task<Optional<I>> task, Converter<I, O> converter)
			=> (await task).Convert(converter);

		public static async Task<O> Convert<I, O>(this Task<I> task, Converter<I, Task<O>> converter)
			=> await converter(await task);
    }
}
