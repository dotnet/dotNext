using System;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Provides task result conversion methods.
    /// </summary>
    public static class Conversion
    {
        /// <summary>
        /// Converts one type of task into another.
        /// </summary>
        /// <typeparam name="I">The source task type.</typeparam>
        /// <typeparam name="O">The target task type.</typeparam>
        /// <param name="task">The task to convert.</param>
        /// <param name="converter">Non-blocking conversion function.</param>
        /// <returns>The converted task.</returns>
		public static async Task<O> Convert<I, O>(this Task<I> task, Converter<I, O> converter)
            => converter(await task.ConfigureAwait(false));

        /// <summary>
        /// Converts one type of task into another.
        /// </summary>
        /// <typeparam name="I">The source task type.</typeparam>
        /// <typeparam name="O">The target task type.</typeparam>
        /// <param name="task">The task to convert.</param>
        /// <param name="converter">Asynchronous conversion function.</param>
        /// <returns>The converted task.</returns>
        public static async Task<O> Convert<I, O>(this Task<I> task, Converter<I, Task<O>> converter)
            => await converter(await task.ConfigureAwait(false));
    }
}
