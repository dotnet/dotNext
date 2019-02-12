using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using static Delegates;

    /// <summary>
    /// Provides set of methods for asynchronous invocation of various delegates.
    /// </summary>
    /// <remarks>
    /// In .NET Core, BeginInvoke and EndInvoke methods of delegate type are not supported.
    /// This class provides alternative approach which allows to invoke delegate asynchronously
    /// with full support of async/await feature.
    /// </remarks>
    /// <seealso href="https://github.com/dotnet/corefx/issues/5940"/>
    public static class AsyncDelegate
    {
        private static Task StartNew<D>(D @delegate, Action<D> invoker, CancellationToken token)
            where D: Delegate
            => Task.Factory.StartNew(() => invoker(@delegate), token);

        private static Task InvokeAsync<D>(D @delegate, Func<D, Task> invoker)
            where D: MulticastDelegate
        {
            if (@delegate is null)
                return Task.CompletedTask;
            var handlers = GetInvocationList(@delegate);
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return invoker(handlers[0]);
                default:
                    return Task.WhenAll(handlers.Select(invoker));
            }
        }

        private static Task InvokeAsync<D>(D @delegate, Action<D> invoker, CancellationToken token)
            where D: MulticastDelegate
            => InvokeAsync(@delegate, h => StartNew(h, invoker, token));

        /// <summary>
        /// Invokes event handlers asynchronously.
        /// </summary>
        /// <typeparam name="E">Type of event object.</typeparam>
        /// <param name="handler">A set event handlers combined as single delegate.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>An object representing state of the asynchronous invocation.</returns>
        public static Task InvokeAsync<E>(this EventHandler<E> handler, object sender, E args, CancellationToken token = default)
            => InvokeAsync(handler, h => h(sender, args), token);

        /// <summary>
        /// Invokes event handlers asynchronously.
        /// </summary>
        /// <param name="handler">A set event handlers combined as single delegate.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>An object representing state of the asynchronous invocation.</returns>
        public static Task InvokeAsync(this EventHandler handler, object sender, EventArgs args, CancellationToken token = default)
            => InvokeAsync(handler, h => h(sender, args), token);

        public static Task InvokeAsync(this Action action, CancellationToken token = default)
            => InvokeAsync(action, h => h(), token);

        public static Task InvokeAsync<T>(this Action<T> action, T arg, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg), token);

        public static Task InvokeAsync<T1, T2>(this Action<T1, T2> action, T1 arg1, T2 arg2, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2), token);

        public static Task InvokeAsync<T1, T2, T3>(this Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3), token);

        public static Task InvokeAsync<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4), token);

        public static Task InvokeAsync<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5), token);

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6>(this Action<T1, T2, T3, T4, T5, T6> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6), token);

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7), token);

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8), token);

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9), token);

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10), token);
    }
}