using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents a destination of data that can be flushed.
    /// </summary>
    public interface IFlushable
    {
        private static readonly Action<object> FlushableFlush = UnsafeFlush;
        private static readonly Func<object, CancellationToken, Task> FlushableFlushAsync = UnsafeFlushAsync;

        /// <summary>
        /// Flushes this stream by writing any buffered output to the underlying stream.
        /// </summary>
        /// <exception cref="System.IO.IOException">I/O error occurred.</exception>
        void Flush();

        /// <summary>
        /// Flushes this stream asynchronously by writing any buffered output to the underlying stream.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        Task FlushAsync(CancellationToken token = default) => Task.Factory.StartNew(Flush, token, TaskCreationOptions.None, TaskScheduler.Current);

        private static void UnsafeFlush(object writer)
        {
            Debug.Assert(writer is IFlushable);
            Unsafe.As<IFlushable>(writer).Flush();
        }

        private static Task UnsafeFlushAsync(object writer, CancellationToken token)
        {
            Debug.Assert(writer is IFlushable);
            return Unsafe.As<IFlushable>(writer).FlushAsync(token);
        }

        /// <summary>
        /// Checks if the specified object implements <see cref="IFlushable"/> interface
        /// and modifies the callbacks.
        /// </summary>
        /// <remarks>
        /// This method is not intended to be used directly in application code.
        /// </remarks>
        /// <param name="obj">The object possibly implementing <see cref="IFlushable"/> interface.</param>
        /// <param name="flush">The delegate representing synchronous flush operation.</param>
        /// <param name="flushAsync">The delegate representing asynchronous flush operation.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void DiscoverFlushMethods<T>(T? obj, ref Action<T>? flush, ref Func<T, CancellationToken, Task>? flushAsync)
            where T : class
        {
            if (obj is IFlushable)
            {
                flush ??= FlushableFlush;
                flushAsync ??= FlushableFlushAsync;
            }
        }
    }
}
