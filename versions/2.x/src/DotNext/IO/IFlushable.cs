using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.IO
{
    /// <summary>
    /// Represents a destination of data that can be flushed.
    /// </summary>
    public interface IFlushable
    {
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

        private static Action<object> ReflectFlushMethod(object obj)
        {
            Debug.Assert(obj is IFlushable);
            Ldnull();
            Push(obj);
            Ldvirtftn(Method(Type<IFlushable>(), nameof(IFlushable.Flush)));
            Newobj(Constructor(Type<Action<object>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<object>>();
        }

        private static Func<object, CancellationToken, Task> ReflectAsyncFlushMethod(object obj)
        {
            Debug.Assert(obj is IFlushable);
            Ldnull();
            Push(obj);
            Ldvirtftn(Method(Type<IFlushable>(), nameof(IFlushable.FlushAsync)));
            Newobj(Constructor(Type<Func<object, CancellationToken, Task>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<object, CancellationToken, Task>>();
        }

        /// <summary>
        /// Creates open delegate for <see cref="Flush"/> method if the specified
        /// object implements <see cref="IFlushable"/> interface.
        /// </summary>
        /// <param name="obj">The instance of the type that potentially implements <see cref="IFlushable"/> interface.</param>
        /// <typeparam name="T">The type that potentially implements <see cref="IFlushable"/> interface.</typeparam>
        /// <returns>Open delegate representing <see cref="Flush"/> method.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Action<T>? TryReflectFlushMethod<T>(T obj)
            where T : class
            => obj is IFlushable ? ReflectFlushMethod(obj) : null;

        /// <summary>
        /// Creates open delegate for <see cref="FlushAsync(CancellationToken)"/> method if the specified
        /// object implements <see cref="IFlushable"/> interface.
        /// </summary>
        /// <param name="obj">The instance of the type that potentially implements <see cref="IFlushable"/> interface.</param>
        /// <typeparam name="T">The type that potentially implements <see cref="IFlushable"/> interface.</typeparam>
        /// <returns>Open delegate representing <see cref="FlushAsync(CancellationToken)"/> method.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Func<T, CancellationToken, Task>? TryReflectAsyncFlushMethod<T>(T obj)
            where T : class
            => obj is IFlushable ? ReflectAsyncFlushMethod(obj) : null;
    }
}
